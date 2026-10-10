using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppSystem.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;

namespace Frontier.Survivor;

internal sealed class NavigationLayer : IDisposable
{
	private sealed class Bake
	{
		internal NavMeshData Data;

		internal AsyncOperation Op;

		internal NavMeshDataInstance Instance;

		internal int AgentId;

		internal bool Published;
	}

	private readonly System.Collections.Generic.List<Bake> bakes = new System.Collections.Generic.List<Bake>();

	private readonly LeaseBag roots = new LeaseBag();

	private readonly Il2CppSystem.Collections.Generic.List<NavMeshBuildSource> sources = new Il2CppSystem.Collections.Generic.List<NavMeshBuildSource>();

	private static readonly System.Collections.Generic.List<NavigationLayer> retired = new System.Collections.Generic.List<NavigationLayer>();

	private bool disposed;

	internal bool Validated { get; private set; }

	internal bool Done
	{
		get
		{
			if (bakes.Count > 0)
			{
				return bakes.All((Bake b) => b.Op.isDone);
			}
			return false;
		}
	}

	internal int AgentCount => bakes.Count;

	public string Fingerprint { get; }

	internal NavigationLayer(LocationDefinition d)
	{
		Fingerprint = d.CollisionFingerprint();
		roots.Keep(sources);
		foreach (BoxDef item in d.Boxes.Where((BoxDef b) => b.Solid))
		{
			sources.Add(new NavMeshBuildSource
			{
				shape = NavMeshBuildSourceShape.Box,
				size = new Vector3(item.Size[0], item.Size[1], item.Size[2]),
				transform = Matrix4x4.TRS(new Vector3(item.Center[0], item.Center[1], item.Center[2]), Quaternion.identity, Vector3.one),
				area = ((!(item.Kind == "floor")) ? 1 : 0)
			});
		}
		BoxDef[] source = d.Boxes.Where((BoxDef b) => b.Solid).ToArray();
		Vector3 vector = new Vector3(source.Min((BoxDef b) => b.Center[0] - b.Size[0] / 2f) - 2f, -2f, source.Min((BoxDef b) => b.Center[2] - b.Size[2] / 2f) - 2f);
		Vector3 vector2 = new Vector3(source.Max((BoxDef b) => b.Center[0] + b.Size[0] / 2f) + 2f, 8f, source.Max((BoxDef b) => b.Center[2] + b.Size[2] / 2f) + 2f);
		int settingsCount = NavMesh.GetSettingsCount();
		if (settingsCount < 1 || settingsCount > 16)
		{
			throw new InvalidOperationException("Native navigation agents unavailable");
		}
		try
		{
			for (int num = 0; num < settingsCount; num++)
			{
				NavMeshBuildSettings settingsByIndex = NavMesh.GetSettingsByIndex(num);
				if (!(settingsByIndex.agentRadius <= 0f) && !(settingsByIndex.agentHeight <= 0f) && !(settingsByIndex.agentRadius > d.Agent.Radius) && !(settingsByIndex.agentHeight > d.Agent.Height))
				{
					NavMeshData navMeshData = new NavMeshData(settingsByIndex.agentTypeID)
					{
						name = "Reserve." + settingsByIndex.agentTypeID,
						position = Vector3.zero,
						rotation = Quaternion.identity
					};
					roots.Keep(navMeshData);
					AsyncOperation asyncOperation = NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, settingsByIndex, sources, new Bounds((vector + vector2) * 0.5f, vector2 - vector));
					if (asyncOperation == null)
					{
						throw new InvalidOperationException("Native NavMesh bake did not start");
					}
					roots.Keep(asyncOperation);
					bakes.Add(new Bake
					{
						Data = navMeshData,
						Op = asyncOperation,
						AgentId = settingsByIndex.agentTypeID
					});
				}
			}
			if (bakes.Count == 0)
			{
				throw new InvalidOperationException("Layout does not fit the game's navigation agents");
			}
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	internal void Publish()
	{
		if (!Done)
		{
			throw new InvalidOperationException("Navigation bake is incomplete");
		}
		if (Validated)
		{
			return;
		}
		foreach (Bake bake in bakes)
		{
			bake.Instance = NavMesh.AddNavMeshData(bake.Data);
			bake.Published = bake.Instance.valid;
			if (!bake.Published)
			{
				throw new InvalidOperationException("Native NavMesh registration failed");
			}
			System.Collections.Generic.Dictionary<string, Vector3> dictionary = new System.Collections.Generic.Dictionary<string, Vector3>();
			Anchor[] anchors = Content.Map.Anchors;
			foreach (Anchor anchor in anchors)
			{
				Vector3 vector = new Vector3(anchor.Position[0], 0f, anchor.Position[2]);
				if (!NavMesh.SamplePositionFilter(vector, out var hit, 1f, bake.AgentId, 1) || Vector3.Distance(vector, hit.position) > 1f)
				{
					throw new InvalidOperationException("Native navigation point unavailable: " + anchor.Id);
				}
				dictionary[anchor.Id] = hit.position;
			}
			foreach (Anchor item in Content.Map.Anchors.Where((Anchor a) => a.Kind == "player"))
			{
				foreach (Anchor item2 in Content.Map.Anchors.Where((Anchor a) => a.Kind == "exit"))
				{
					CheckPath(dictionary[item.Id], dictionary[item2.Id], bake.AgentId);
				}
			}
		}
		Validated = true;
		Trace.Data["nativeNavigationValidated"] = new
		{
			agents = bakes.Select((Bake b) => b.AgentId).ToArray(),
			anchorChecks = Content.Map.Anchors.Length * bakes.Count,
			extractionRoutes = 8 * bakes.Count
		};
	}

	private unsafe static void CheckPath(Vector3 from, Vector3 to, int agentId)
	{
		NavMeshQuery navMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Temp, 8192);
		try
		{
			NavMeshLocation navMeshLocation = navMeshQuery.MapLocation(from, Vector3.one, agentId, 1);
			NavMeshLocation navMeshLocation2 = navMeshQuery.MapLocation(to, Vector3.one, agentId, 1);
			if (!navMeshQuery.IsValid(navMeshLocation) || !navMeshQuery.IsValid(navMeshLocation2))
			{
				throw new InvalidOperationException("Invalid path endpoint");
			}
			PathQueryStatus pathQueryStatus = NavMeshQuery.BeginFindPath(navMeshQuery.m_NavMeshQuery, navMeshLocation, navMeshLocation2, 1, null);
			int num = 0;
			while ((pathQueryStatus & PathQueryStatus.InProgress) != 0 && num < 32768)
			{
				pathQueryStatus = navMeshQuery.UpdateFindPath(1024, out var iterationsPerformed);
				num += Math.Max(1, iterationsPerformed);
			}
			if ((pathQueryStatus & PathQueryStatus.Success) == 0 || (pathQueryStatus & (PathQueryStatus.Failure | PathQueryStatus.OutOfNodes | PathQueryStatus.PartialResult)) != 0)
			{
				throw new InvalidOperationException("Native extraction path search failed");
			}
			pathQueryStatus = navMeshQuery.EndFindPath(out var pathSize);
			if ((pathQueryStatus & PathQueryStatus.Success) == 0 || pathSize < 1 || pathSize > 8192 || (pathQueryStatus & PathQueryStatus.PartialResult) != 0)
			{
				throw new InvalidOperationException("Native extraction path is partial");
			}
			PolygonId* ptr = stackalloc PolygonId[pathSize];
			if (NavMeshQuery.GetPathResult(navMeshQuery.m_NavMeshQuery, ptr, pathSize) != pathSize || !ptr[pathSize - 1].Equals(navMeshLocation2.polygon))
			{
				throw new InvalidOperationException("Native extraction path endpoint mismatch");
			}
		}
		finally
		{
			navMeshQuery.Dispose();
		}
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		disposed = true;
		foreach (Bake bake in bakes)
		{
			if (!bake.Op.isDone)
			{
				NavMeshBuilder.Cancel(bake.Data);
			}
			if (bake.Published)
			{
				bake.Instance.Remove();
				bake.Published = false;
			}
		}
		if (bakes.Any((Bake b) => !b.Op.isDone))
		{
			retired.Add(this);
		}
		else
		{
			Free();
		}
	}

	private void Free()
	{
		foreach (Bake bake in bakes)
		{
			if (bake.Data != null)
			{
				UnityEngine.Object.Destroy(bake.Data);
			}
		}
		bakes.Clear();
		roots.Dispose();
	}

	internal static void CollectRetired()
	{
		for (int num = retired.Count - 1; num >= 0; num--)
		{
			if (retired[num].bakes.All((Bake b) => b.Op.isDone))
			{
				retired[num].Free();
				retired.RemoveAt(num);
			}
		}
	}
}
