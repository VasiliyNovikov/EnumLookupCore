using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using BenchmarkDotNet.Attributes;

namespace EnumLookupCore.Benchmarks;

[ShortRunJob]
public class LookupBenchmarks
{
    private enum Test
    {
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P
    }

    private static readonly Test[] Values = Enum.GetValues<Test>();
    private static readonly string[] LowerNames = Values.Select(value => value.ToString().ToLowerInvariant()).ToArray();
    private static readonly EnumDictionary<Test, string> Table = EnumDictionary.LoverCaseNames<Test>();
    private static readonly FrozenDictionary<Test, string> Frozen = Values.ToFrozenDictionary(key => key, key => key.ToString().ToLowerInvariant());
    private static readonly Dictionary<Test, string> Dict = Values.ToDictionary(key => key, key => key.ToString().ToLowerInvariant()); 

    [Benchmark]
    public string Array_LowerNames()
    {
        var table = LowerNames;
        var result = string.Empty;
        foreach (var value in Values)
            result = table[(int)value];
        return result;
    }

    [Benchmark]
    public string Table_LowerNames()
    {
        var table = Table;
        var result = string.Empty;
        foreach (var value in Values)
            result = table[value];
        return result;
    }

    [Benchmark]
    public string Dict_LowerNames()
    {
        var table = Dict;
        var result = string.Empty;
        foreach (var value in Values)
            result = table[value];
        return result;
    }

    [Benchmark]
    public string Frozen_LowerNames()
    {
        var table = Frozen;
        var result = string.Empty;
        foreach (var value in Values)
            result = table[value];
        return result;
    }
}