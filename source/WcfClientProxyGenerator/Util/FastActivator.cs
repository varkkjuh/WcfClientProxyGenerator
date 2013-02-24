﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace WcfClientProxyGenerator.Util
{
    internal static class FastActivator
    {
        private static readonly ConcurrentDictionary<int, Func<object[], object>> ActivatorCache 
            = new ConcurrentDictionary<int, Func<object[], object>>(); 

        public static object CreateInstance(Type type, params object[] args)
        {
            int offset = type.GetHashCode();
            int key = args.Aggregate(0, (x, o) => x ^ (o == null ? offset : o.GetType().GetHashCode() << offset++));

            var activator = ActivatorCache.GetOrAdd(key, (int _) => BuildActivatorLambda(type, args));

            return activator(args);
        }

        public static T CreateInstance<T>()
            where T : class, new()
        {
            return new T();
        }

        public static T CreateInstance<T>(params object[] args)
            where T : class
        {
            return CreateInstance(typeof(T), args) as T;
        }

        private static Func<object[], object> BuildActivatorLambda(Type type, IEnumerable<object> args)
        {
            var parameterTypes = args.Select(m => m.GetType()).ToArray();

            var constructorInfo = type.GetConstructor(parameterTypes);
            if (constructorInfo == null)
                throw new Exception(
                    string.Format("Could not locate constructor on type {0} with params: {1}", 
                        type.Name, 
                        string.Join(", ", parameterTypes.Select(t => t.Name))));

            var parameters = constructorInfo.GetParameters();

            var parameterExpression = Expression.Parameter(typeof(object[]), "args");


            var argumentExpression = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var index = Expression.Constant(i);
                var parameterType = parameters[i].ParameterType;

                var parameterAccessorExpression = Expression.ArrayIndex(parameterExpression, index);

                var parameterCastExpression = Expression.Convert(parameterAccessorExpression, parameterType);

                argumentExpression[i] = parameterCastExpression;
            }

            var newExpression = Expression.New(constructorInfo, argumentExpression);
            var lambda = Expression.Lambda(typeof(Func<object[], object>), newExpression, parameterExpression);

            return (Func<object[], object>) lambda.Compile();
        }
    }
}
