using System.Reflection;
using Serilog.Core;
using Serilog.Events;
using SaveSphere.SharedKernel;

namespace SaveSphere.Infrastructure.Logging;

/// <summary>
/// Masks <see cref="IRedacted"/> values and any property marked <see cref="SensitiveAttribute"/>
/// when an object is destructured into a log event (<c>{@Value}</c>).
/// </summary>
public sealed class SensitiveDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        if (value is IRedacted)
        {
            result = new ScalarValue(LogRedactor.Mask);
            return true;
        }

        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToArray();

        if (!properties.Any(p => p.GetCustomAttribute<SensitiveAttribute>() is not null))
        {
            result = null!;
            return false;
        }

        result = new StructureValue(
            properties.Select(p => new LogEventProperty(
                p.Name,
                p.GetCustomAttribute<SensitiveAttribute>() is not null
                    ? new ScalarValue(LogRedactor.Mask)
                    : propertyValueFactory.CreatePropertyValue(p.GetValue(value), destructureObjects: true))),
            type.Name);
        return true;
    }
}
