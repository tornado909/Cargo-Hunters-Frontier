using System;

namespace Frontier.Loot;

public sealed class Bonuses
{
	public float Damage;

	public float Crit;

	public float CritPower;

	public float SaveAmmo;

	public float FireRate;

	public int Penetration;

	public bool Executor;

	public int Count;

	public bool HasLadder;

	public void ClampLadder(bool enemy = false)
	{
		if (HasLadder)
		{
			Damage = Math.Clamp(Damage, 0f, 16f);
			Crit = Math.Clamp(Crit, 0f, 5f);
			CritPower = Math.Clamp(CritPower, 0f, 8f);
			SaveAmmo = Math.Clamp(SaveAmmo, 0f, 0.95f);
			FireRate = Math.Clamp(FireRate, 0f, 2f);
			Penetration = Math.Clamp(Penetration, 0, 32);
		}
	}

	public void Add(Trait t)
	{
		HasLadder |= t.RankIndex > 0;
		Damage += t.Damage;
		Crit += t.Crit;
		CritPower += t.CritPower;
		SaveAmmo += t.SaveAmmo;
		FireRate += t.FireRate;
		Penetration += t.Penetration;
		Executor |= t.Executor;
		Count++;
	}
}
