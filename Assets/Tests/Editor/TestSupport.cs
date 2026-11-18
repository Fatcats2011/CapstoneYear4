using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DoA.Tests
{
    /// <summary>
    /// Creates throwaway GameObjects for EditMode tests and destroys them afterwards.
    /// Components added in Edit Mode don't get Awake/OnEnable/Start, so tests call those directly (the game's members they reach are internal).
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

        /// <summary>Destroys a GameObject made elsewhere along with the rest</summary>
        public void Keep(GameObject go)
        {
            created.Add(go);
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
    /// Reaches what direct access can't: event subscriber lists (a C# event's backing field) and another package's private methods.
    /// </summary>
    public static class Reflect
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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

        /// <summary>
        /// How many handlers on an event (or delegate field) belong to the subscriber
        /// </summary>
        public static int HandlerCount(object source, string eventName, object subscriber)
        {
            Delegate handlers = FindField(source.GetType(), eventName).GetValue(source) as Delegate;
            return handlers == null ? 0 : handlers.GetInvocationList().Count(h => ReferenceEquals(h.Target, subscriber));
        }

        /// <summary>
        /// Raises an event (or calls a delegate field) from outside its class: every handler, as the class would
        /// </summary>
        public static void Raise(object source, string eventName, params object[] args)
        {
            Delegate handlers = FindField(source.GetType(), eventName).GetValue(source) as Delegate;
            try
            {
                handlers?.DynamicInvoke(args);
            }
            catch (TargetInvocationException e)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
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

    /// <summary>
    /// The helpers many test classes share: the game's state, its players and scooters, waiting, and input. A test
    /// class reaches them by name with "using static DoA.Tests.TestHelpers;"; one that needs its own version of a
    /// helper declares it, and that one is used there
    /// </summary>
    public static class TestHelpers
    {
        public static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        public static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        public static string ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        public static void NewEmptyScene()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        public static IEnumerator Wait(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        public static IEnumerator WaitSeconds(float seconds)
        {
            return Wait(seconds);
        }

        /// <summary>
        /// Queues a press and release; the next input update sees a full button press
        /// </summary>
        public static void Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        public static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        public static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
        }

        // A player on this machine drives their ball there and stops
        public static void PutBallAt(int seat, Vector3 spot)
        {
            Rigidbody ball = Slot(seat).Player.GetComponentInChildren<Rigidbody>();
            ball.velocity = Vector3.zero;
            ball.position = spot;
            ball.transform.position = spot;
        }

        // Where a player who finished the tutorial waits: a city respawn point far from every order's beacons
        public static Vector3 CitySpot()
        {
            Vector3 best = Vector3.zero;
            float bestGap = -1f;
            Order[] orders = UnityEngine.Object.FindObjectsOfType<Order>(true);
            foreach (RespawnPoint point in UnityEngine.Object.FindObjectsOfType<RespawnPoint>())
            {
                float gap = float.MaxValue;
                foreach (Order order in orders)
                {
                    if (order.PickupPoint != null)
                        gap = Mathf.Min(gap, Vector3.Distance(point.transform.position, order.PickupPoint.position));
                    if (order.DropoffPoint != null)
                        gap = Mathf.Min(gap, Vector3.Distance(point.transform.position, order.DropoffPoint.position));
                }
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = point.transform.position;
                }
            }
            return best;
        }

        // A player on this machine finishes the tutorial and waits in the city, as a player who drove out of it does
        // (the tutorial area goes a second into the first wave)
        public static void FinishTutorialInTheCity(int seat)
        {
            PutBallAt(seat, CitySpot());
            Slot(seat).Player.GetComponentInChildren<TutorialHandler>().TeachHandler(TutorialType.Final);
        }

        public static bool Sees(OnlineSession session, int players)
        {
            return session.Match != null && session.Players.Count == players;
        }

        public static OnlinePlayer PlayerInSeat(OnlineSession session, int seat)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.Seat == seat)
                    return player;
            }
            return null;
        }

        public static OnlineScooter ScooterInSeat(OnlineSession session, int seat)
        {
            foreach (OnlineScooter scooter in session.Scooters)
            {
                if (scooter.Seat == seat)
                    return scooter;
            }
            return null;
        }

        // The machine that owns a scooter moves it there, as its OnlineDriving would
        public static void ShareAt(OnlineScooter scooter, Vector3 spot)
        {
            scooter.Share(new ScooterPose { Ball = spot, Heading = 0, ModelPosition = spot, ModelRotation = Quaternion.identity }, default(DriveFlags));
        }
    }
}
