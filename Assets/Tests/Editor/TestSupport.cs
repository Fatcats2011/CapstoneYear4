using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Creates throwaway GameObjects for EditMode tests and destroys them afterwards.
    /// Components added in Edit Mode don't get Awake/OnEnable/Start, so tests call those through Reflect.
    /// </summary>
    public class TestObjects
    {
        readonly List<GameObject> created = new List<GameObject>();

        public GameObject NewGameObject(string name = "Test Object")
        {
            GameObject go = new GameObject(name);
            created.Add(go);
            return go;
        }

        public T Add<T>() where T : Component
        {
            return NewGameObject(typeof(T).Name).AddComponent<T>();
        }

        public void DestroyAll()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            created.Clear();
        }
    }

    /// <summary>
    /// Reaches private fields, lifecycle methods, singleton instances and event subscriber lists for tests.
    /// </summary>
    public static class Reflect
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void SetField(object target, string name, object value)
        {
            FindField(target.GetType(), name).SetValue(target, value);
        }

        public static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, args.Length);
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException e)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        public static void SetSingleton<T>(T instance) where T : MonoBehaviour
        {
            typeof(SingletonMonobehaviour<T>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, instance);
        }

        /// <summary>
        /// How many handlers on an event (or delegate field) belong to the subscriber
        /// </summary>
        public static int HandlerCount(object source, string eventName, object subscriber)
        {
            Delegate handlers = FindField(source.GetType(), eventName).GetValue(source) as Delegate;
            return handlers == null ? 0 : handlers.GetInvocationList().Count(h => ReferenceEquals(h.Target, subscriber));
        }

        static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, Members);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.Name, name);
        }

        static MethodInfo FindMethod(Type type, string name, int argumentCount)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo method = t.GetMethods(Members).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == argumentCount);
                if (method != null)
                    return method;
            }
            throw new MissingMethodException(type.Name, name);
        }
    }
}
