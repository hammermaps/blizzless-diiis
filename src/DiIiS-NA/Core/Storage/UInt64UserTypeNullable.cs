using System;
using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

namespace DiIiS_NA.Core.Storage
{
	/// <summary>
	/// Dialect-neutral NHibernate user type that maps a nullable CLR <see cref="UInt64"/> to
	/// the underlying SQL <c>BIGINT</c> (signed) column.  A value of <c>0</c> is persisted as
	/// SQL <c>NULL</c>, which allows NHibernate to detect unsaved entities when combined with
	/// <c>.UnsavedValue(null)</c> on the identity mapping.
	/// </summary>
	public class UInt64UserTypeNullable : IUserType
	{
		public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
		{
			var value = NHibernateUtil.Int64.NullSafeGet(rs, names[0], session);
			return (value == null) ? 0 : Convert.ToUInt64(value);
		}

		public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
		{
			if (value == null || Convert.ToInt64(value) == 0)
				NHibernateUtil.Int64.NullSafeSet(cmd, null, index, session);
			else
				NHibernateUtil.Int64.NullSafeSet(cmd, Convert.ToInt64(value), index, session);
		}

		public Type ReturnedType
		{
			get { return typeof(UInt64); }
		}

		public SqlType[] SqlTypes
		{
			get { return new[] { SqlTypeFactory.Int64 }; }
		}

		public new bool Equals(object x, object y)
		{
			if (x == y) return true;

			ulong lhs = (x == null) ? 0 : (ulong)x;
			ulong rhs = (y == null) ? 0 : (ulong)y;
			return lhs.Equals(rhs);
		}

		public int GetHashCode(object x)
		{
			return (x == null) ? 0 : x.GetHashCode();
		}

		public object DeepCopy(object value)
		{
			return value;
		}

		public bool IsMutable
		{
			get { return false; }
		}

		public object Replace(object original, object target, object owner)
		{
			return original;
		}

		public object Assemble(object cached, object owner)
		{
			return cached;
		}

		public object Disassemble(object value)
		{
			return value;
		}
	}
}
