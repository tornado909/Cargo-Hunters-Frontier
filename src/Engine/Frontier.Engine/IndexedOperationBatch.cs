#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Frontier.Engine;

// Transformation-local index: never shared with callers or kept between repositories.
// Public ApplyOperation remains the compatibility reference for arbitrary callers.
internal sealed class IndexedOperationBatch
{
    internal JsonNode Root { get; private set; }
    JsonArray? indexedArray;
    string? indexedKey;
    Dictionary<string,List<int>>? ids;
    int indexedCount;
    internal int FullScans { get; private set; }
    internal int IndexedLookups { get; private set; }
    internal IndexedOperationBatch(JsonNode root) => Root=root;
    void Invalidate() { indexedArray=null; indexedKey=null; ids=null; indexedCount=0; }
    void EnsureIndex(JsonArray array,string key)
    {
        if(ReferenceEquals(indexedArray,array)&&indexedKey==key&&indexedCount==array.Count&&ids!=null)return;
        var next=new Dictionary<string,List<int>>(StringComparer.Ordinal);
        // The original source lookup visits every row too. Missing/null keys must fail,
        // and duplicate source keys must be counted, not silently discarded.
        for(int i=0;i<array.Count;i++)
        {
            string id=PatchEngine.Scalar(array[i]?[key]);
            if(!next.TryGetValue(id,out var positions))next.Add(id,positions=new List<int>(1));
            positions.Add(i);
        }
        indexedArray=array;indexedKey=key;indexedCount=array.Count;ids=next;FullScans++;
    }
    internal JsonNode Apply(JsonObject operation)
    {
        string kind=PatchEngine.Str(operation,"op");
        if(kind=="root"||kind=="append")
        {
            Invalidate();
            Root=PatchEngine.ApplyOperation(Root,operation);
            return Root;
        }
        if(Root is not JsonArray array)throw new InvalidDataException("Expected repository array");
        string key=PatchEngine.Str(operation,"key"),source=PatchEngine.Scalar(operation["source"]);
        EnsureIndex(array,key);IndexedLookups++;
        ids!.TryGetValue(source,out var matches);
        int count=matches?.Count??0;
        if(count!=1)throw new InvalidDataException($"Expected one {key}={source}, got {count}");
        int position=matches![0];
        if(kind=="clone")
        {
            string id=PatchEngine.Scalar(operation["id"]);
            if(ids.ContainsKey(id))throw new InvalidDataException("Duplicate clone "+id);
            var value=PatchEngine.ApplyEdits(PatchEngine.Copy(array[position])!, (JsonArray)operation["edits"]!);
            if(PatchEngine.Scalar(value[key])!=id)throw new InvalidDataException("Clone ID differs from declared ID");
            int target=array.Count;array.Add(value);
            ids.Add(id,new List<int>(1){target});indexedCount=array.Count;
        }
        else
        {
            if(kind!="edit")throw new InvalidDataException("Unknown operation: "+kind);
            var value=PatchEngine.ApplyEdits(PatchEngine.Copy(array[position])!, (JsonArray)operation["edits"]!);
            array[position]=value;
            string newId;
            try { newId=PatchEngine.Scalar(value[key]); }
            catch(InvalidDataException) { Invalidate();return Root; }
            catch(InvalidOperationException) { Invalidate();return Root; }
            // An edit may change/remove its key, including creating duplicates. Preserve
            // that behavior; only a subsequent lookup or original final check can reject it.
            matches.Remove(position);if(matches.Count==0)ids.Remove(source);
            if(!ids.TryGetValue(newId,out var positions))ids.Add(newId,positions=new List<int>(1));
            positions.Add(position);
        }
        return Root;
    }
}
