using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Rebus.Sagas;

namespace Rebus.Redis.Sagas;

internal class SagaPropertyAccessor
{
    private readonly ConcurrentDictionary<(Type sageType, string property), Func<ISagaData, object>> _getValuesCache =
        new();

    public object? GetValueFromSagaData(ISagaData data, ISagaCorrelationProperty property)
    {
        var accessor = _getValuesCache.GetOrAdd((data.GetType(), property.PropertyName),CreateAccessor);
        return accessor(data);
    }

    private Func<ISagaData, object> CreateAccessor((Type sageType, string propertyName) arg)
    {
        var (type, property) = arg;
        var propertyInfo = type.GetProperty(property);
        if (propertyInfo == null) throw new ArgumentException($"Saga data type {type.Name} does not have a property {property}");
        var parameter = Expression.Parameter(typeof(ISagaData));
        var convertedParameter = Expression.Convert(parameter, type);
        var propertyAccess = Expression.Property(convertedParameter, propertyInfo);
        var cast = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<ISagaData, object>>(cast, parameter).Compile();
    }
}