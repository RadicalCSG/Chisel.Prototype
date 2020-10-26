/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Core.ChiselReflectionUtility.cs

License:
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Chisel.Core
{
    public static class ChiselReflectionUtility
    {
        private const BindingFlags ALL_FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Find a type by name, and optionally an assembly type. Type should include namespace.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type GetType( string type, string assembly = null )
        {
            Type t = Type.GetType( type );

            if( t == null )
            {
                IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies();

                if( assembly != null ) assemblies = assemblies.Where( x => x.FullName.Contains( assembly ) );

                foreach( Assembly asm in assemblies )
                {
                    t = asm.GetType( type );

                    if( t != null ) return t;
                }
            }

            return t;
        }

        /// <summary>
        /// Get a value from either a property or field
        /// </summary>
        /// <param name="target"></param>
        /// <param name="type"></param>
        /// <param name="member"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static object GetValue( object target, string type, string member, BindingFlags flags = ALL_FLAGS )
        {
            Type t = GetType( type );

            if( t == null )
            {
                Debug.LogError( $"Could not find the type '{type}'" );
                return null;
            }
            else return GetValue( target, t, member, flags );
        }

        public static object GetValue( object target, Type type, string member, BindingFlags flags = ALL_FLAGS )
        {
            PropertyInfo pi = type.GetProperty( member, flags );

            if( pi != null ) return pi.GetValue( target, null );

            FieldInfo fi = type.GetField( member, flags );

            if( fi != null ) return fi.GetValue( target );

            return null;
        }

        /// <summary>
        /// Set a property or field
        /// </summary>
        /// <param name="target"></param>
        /// <param name="member"></param>
        /// <param name="value"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static bool SetValue( object target, string member, object value, BindingFlags flags = ALL_FLAGS )
        {
            if( target == null ) return false;

            PropertyInfo pi = target.GetType().GetProperty( member, flags );

            if(pi != null) pi.SetValue( target, value, flags, null, null, CultureInfo.InvariantCulture );

            FieldInfo fi = target.GetType().GetField( member, flags );

            if(fi != null) fi.SetValue( target, value );

            return pi != null || fi != null;
        }

        public static MethodInfo GetMethod( string type, string method, BindingFlags flags = ALL_FLAGS )
        {
            Type t = GetType( type );

            if( t != null ) return t.GetMethod( method, flags );

            return null;
        }

        public static MethodInfo GetMethod( Type type, string method, BindingFlags flags = ALL_FLAGS )
        {
            if( type != null ) return type.GetMethod( method, flags );

            return null;
        }
    }
}
