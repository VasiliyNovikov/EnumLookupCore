using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace EnumLookupCore;

public abstract class EnumDictionary<TEnum, TValue>
    where TEnum : unmanaged, Enum
    where TValue : notnull
{
    public abstract TValue this[TEnum key] { get; }
}

public static class EnumDictionary
{
    public static EnumDictionary<TEnum, TValue> Create<TEnum, TValue>(Func<TEnum, TValue?> valueFactory)
        where TEnum : unmanaged, Enum
        where TValue : notnull
    {
        return Small<TEnum>.IsSupported
            ? new Small<TEnum>.Table<TValue>(valueFactory)
            : new Large<TEnum>.Table<TValue>(valueFactory);
    }
    
    public static EnumDictionary<TEnum, TValue> CreateDefined<TEnum, TValue>(Func<TEnum, TValue> valueFactory)
        where TEnum : unmanaged, Enum
        where TValue : notnull
    {
        return Create<TEnum, TValue>(key => Enum.IsDefined(key) ? valueFactory(key) : default);
    }
    
    public static EnumDictionary<TEnum, string> Names<TEnum>()
        where TEnum : unmanaged, Enum
    {
        return CreateDefined<TEnum, string>(key => key.ToString());
    }

    public static EnumDictionary<TEnum, string> LoverCaseNames<TEnum>()
        where TEnum : unmanaged, Enum
    {
        return CreateDefined<TEnum, string>(key => key.ToString().ToLowerInvariant());
    }

    public static EnumDictionary<TEnum, TEnum[]> FlagCombinations<TEnum>() where TEnum : unmanaged, Enum
    {
        var values = Enum.GetValues<TEnum>();
        return Create<TEnum, TEnum[]>(key =>
        {
            TEnum[] flags =  [.. values.Where(v => key.HasFlag(v))];
            return flags.Aggregate(0u, (acc, v) => acc | ToUInt32(v)) == ToUInt32(key) ? flags : null;
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToUInt32<TEnum>(TEnum value) where TEnum : unmanaged, Enum
    {
        return Unsafe.SizeOf<TEnum>() switch
        {
            1 => Unsafe.As<TEnum, byte>(ref value),
            2 => Unsafe.As<TEnum, ushort>(ref value),
            4 => Unsafe.As<TEnum, uint>(ref value),
            8 => (uint)Unsafe.As<TEnum, ulong>(ref value),
            _ => throw new NotSupportedException()
        };
    }

    private static class Small<TEnum> where TEnum : unmanaged, Enum
    {
        private const int MaxLength = ushort.MaxValue;
        public static readonly bool IsSupported;
        private static readonly int Length;
        private static readonly TEnum[] Values = null!;

        static Small()
        {
            var definedValues = Enum.GetValues<TEnum>();
            
            ulong maxUInt64 = 0;
            foreach (var value in definedValues)
            {
                var valueCopy = value;
                var uint64Value = Unsafe.SizeOf<TEnum>() switch
                {
                    8 => Unsafe.As<TEnum, ulong>(ref valueCopy),
                    4 => Unsafe.As<TEnum, uint>(ref valueCopy),
                    2 => Unsafe.As<TEnum, ushort>(ref valueCopy),
                    1 => Unsafe.As<TEnum, byte>(ref valueCopy),
                    _ => throw new NotSupportedException()
                };
                maxUInt64 = Math.Max(maxUInt64, uint64Value);
            }

            IsSupported = maxUInt64 < MaxLength;
            if (!IsSupported)
                return;

            Length = (int)BitOperations.RoundUpToPowerOf2(maxUInt64 + 1);
            Values = new TEnum[Length];
            for (var i = 0; i < Length; ++i)
                Values[i] = ToEnum(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TEnum ToEnum(int value)
        {
            switch (Unsafe.SizeOf<TEnum>())
            {
                case 8:
                    var longValue = (long)value;
                    return Unsafe.As<long, TEnum>(ref longValue);
                case 4:
                    return Unsafe.As<int, TEnum>(ref value);
                case 2:
                    var ushortValue = (ushort)value;
                    return Unsafe.As<ushort, TEnum>(ref ushortValue);
                case 1:
                    var byteValue = (byte)value;
                    return Unsafe.As<byte, TEnum>(ref byteValue);
                default:
                    throw new NotSupportedException();
            }
        }

        internal sealed class Table<TValue> : EnumDictionary<TEnum, TValue> where TValue : notnull
        {
            private readonly TValue?[] _values;

            public override TValue this[TEnum key] => _values[ToUInt32(key)] ?? throw new KeyNotFoundException();

            public Table(Func<TEnum, TValue?> valueFactory)
            {
                _values = new TValue[Length];
                for (var i = 0; i < _values.Length; ++i)
                    _values[i] = valueFactory(Values[i]);
            }
        }
    }

    private static class Large<TEnum> where TEnum : unmanaged, Enum
    {
        internal sealed class Table<TValue>(Func<TEnum, TValue?> valueFactory) : EnumDictionary<TEnum, TValue> where TValue : notnull
        {
#if NET9_0_OR_GREATER
            private readonly Lock _lock = new();
#else
            private readonly object _lock = new();
#endif
            private readonly Dictionary<TEnum, TValue?> _values = [];

            public override TValue this[TEnum key]
            {
                get
                {
                    lock (_lock)
                    {
                        ref var valueRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_values, key, out var exists);
                        if (!exists)
                            valueRef = valueFactory(key);
                        return valueRef ?? throw new KeyNotFoundException();
                    }
                }
            }
        }
    }
}