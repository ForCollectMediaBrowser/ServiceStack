﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServiceStack
{
    //Interfaces and DTO's used in AutoQuery
    public interface IQuery
    {
        int? Skip { get; set; }
        int? Take { get; set; }
        string OrderBy { get; set; }
        string OrderByDesc { get; set; }
    }

    public interface IQuery<From> : IQuery { }
    public interface IQuery<From,Into> : IQuery { }

    public interface IJoin { }
    public interface IJoin<Source, Join1> : IJoin { }
    public interface IJoin<Source, Join1, Join2> : IJoin { }
    public interface IJoin<Source, Join1, Join2, Join3> : IJoin { }
    public interface IJoin<Source, Join1, Join2, Join3, Join4> : IJoin { }

    public interface ILeftJoin<Source, Join1> : IJoin { }
    public interface ILeftJoin<Source, Join1, Join2> : IJoin { }
    public interface ILeftJoin<Source, Join1, Join2, Join3> : IJoin { }
    public interface ILeftJoin<Source, Join1, Join2, Join3, Join4> : IJoin { }

    public enum QueryType
    {
        Default = 0,
        And = 1,
        Or = 2,
    }
    public enum ValueStyle
    {
        Single = 0,
        Multiple = 1,
        List = 2,
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class QueryAttribute : AttributeBase
    {
        public QueryAttribute() {}

        public QueryAttribute(QueryType defaultType)
        {
            DefaultType = defaultType;
        }

        public QueryType DefaultType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class QueryFieldAttribute : AttributeBase
    {
        public QueryType Type { get; set; }
        public string Operand { get; set; }
        public string Format { get; set; }
        public string Field { get; set; }
        public string ValueFormat { get; set; }
        public ValueStyle ValueStyle { get; set; }
        public int ValueArity { get; set; }
    }

    public abstract class QueryBase : IQuery
    {
        [DataMember(Order = 1)]
        public int? Skip { get; set; }

        [DataMember(Order = 2)]
        public int? Take { get; set; }

        [DataMember(Order = 3)]
        public string OrderBy { get; set; }

        [DataMember(Order = 4)]
        public string OrderByDesc { get; set; }
    }

    public abstract class QueryBase<T> : QueryBase, IQuery<T>, IReturn<QueryResponse<T>> { }

    public abstract class QueryBase<From, Into> : QueryBase, IQuery<From, Into>, IReturn<QueryResponse<Into>> { }

    [DataContract]
    public class QueryResponse<T> : IHasResponseStatus, IMeta
    {
        [DataMember(Order = 1)]
        public int Offset { get; set; }

        [DataMember(Order = 2)]
        public int Total { get; set; }

        [DataMember(Order = 3)]
        public List<T> Results { get; set; }

        [DataMember(Order = 4)]
        public Dictionary<string, string> Meta { get; set; }

        [DataMember(Order = 5)]
        public ResponseStatus ResponseStatus { get; set; }
    }
}