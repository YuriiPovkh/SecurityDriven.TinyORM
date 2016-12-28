﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SecurityDriven.TinyORM
{
	using Utils;

	#region class RowStore
	public class RowStore : System.Dynamic.DynamicObject, IEnumerable<KeyValuePair<string, object>>
	{
		static readonly FieldNotFound notFound = new FieldNotFound();
		public readonly ResultSetSchema Schema;
		public readonly object[] RowValues;

		internal RowStore(ResultSetSchema schema, object[] rowValues)
		{
			this.Schema = schema;
			this.RowValues = rowValues;
		}

		public object this[int i]
		{
			get
			{
				return this.RowValues[i];
			}
		}

		public object this[string key]
		{
			get
			{
				int index;
				if (!TryGetIndex(key, out index))
				{
					return notFound;
				}
				var result = this.RowValues[index];
				return result == DBNull.Value ? null : result;
			}//get
			set
			{
				int index;
				if (!TryGetIndex(key, out index))
				{
					throw new ArgumentException("\"" + key + "\" column is not found.");
				}
				this.RowValues[index] = value ?? DBNull.Value;
			}//set
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryGetIndex(string key, out int index)
		{
			if (this.Schema.FieldMap.TryGetValue(key, out index))
				return true;

			return false;
		}// TryGetIndex()

		public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
		{
			foreach (var kvp in this.Schema.FieldMap)
			{
				yield return new KeyValuePair<string, object>(kvp.Key, this[kvp.Key]);
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result)
		{
			result = this[binder.Name];
			return true;
		}

		public override string ToString()
		{
			return string.Join(Environment.NewLine, this);
		}

		/// <summary>
		/// Converts RowStore into an instance of T on a best-effort-match basis. Does not throw on any mismatches.
		/// </summary>
		public T ToObject<T>() where T : class, new()
		{
			var setters = ReflectionHelper.GetPropertySetters(typeof(T));
			var result = New<T>.Instance();
			object val;
			Action<object, object> setter;

			foreach (var kvp in this.Schema.FieldMap)
			{
				if (setters.TryGetValue(kvp.Key, out setter))
				{
					val = this[kvp.Key];
					setter(result, val);
				}
			}
			return result;
		}// ToObject<T>()

		/// <summary>
		/// Converts RowStore into an instance of T, and checks existence of all required (non-optional) properties. Failed checks throw an exception.
		/// </summary>
		public T ToCheckedObject<T>(string[] optionalProperties = null) where T : class, new()
		{
			var setters = ReflectionHelper.GetPropertySetters(typeof(T));
			var result = New<T>.Instance();
			HashSet<string> optionalPropertyHashSet = null;

			foreach (var kvp in setters)
			{
				var val = this[kvp.Key];
				if (val != notFound)
					kvp.Value(result, val);
				else
				{
					if (optionalPropertyHashSet == null)
					{
						if (optionalProperties == null || optionalProperties.Length == 0) goto THROW;
						optionalPropertyHashSet = new HashSet<string>(optionalProperties, Util.FastStringComparer.Instance);
					}
					if (optionalPropertyHashSet.Contains(kvp.Key)) { continue; }
					THROW: throw new Exception(string.Format("RowStore has no match for class [{0}] property [{1}].", typeof(T), kvp.Key));
				}
			}
			return result;
		}// ToCheckedObject<T>()
	}// class RowStore
	#endregion

	#region FieldNotFound
	public class FieldNotFound
	{
		internal FieldNotFound() { }
		public override string ToString()
		{
			throw new NotImplementedException(@"Field not found. Use ""obj is FieldNotFound"" instead of .ToString().");
		}
	}//class FieldNotFound
	#endregion
}//ns