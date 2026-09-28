using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// A player's compass drops the markers whose object is gone (an order delivered, the match scene unloaded): all of
    /// them in one frame, without an error. Online, a client can be back in the menu for a few frames before the host's
    /// Menu state resets its compass. Enters Play Mode (a few seconds): the compass destroys the dropped icons
    /// </summary>
    public class CompassTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        // One frame of the compass, and what it threw (null if nothing)
        static Exception UpdateOnce(Compass compass)
        {
            try
            {
                Reflect.Invoke(compass, "Update");
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        [UnityTest]
        public IEnumerator MarkersWhoseObjectIsGone_AreAllDropped_InOneFrame()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            GameObject holder = new GameObject("Compass");
            holder.SetActive(false); // its OnEnable listens to the game's managers, which this empty scene hasn't got
            Compass compass = holder.AddComponent<Compass>();
            List<CompassInformationInstance> markers = (List<CompassInformationInstance>)Reflect.GetField(compass, "compassInformationObjects");
            for (int i = 0; i < 3; i++)
                markers.Add(new CompassInformationInstance(new GameObject("Icon " + i).AddComponent<CompassIconUI>(), null)); // the marked object is gone

            Exception thrown = UpdateOnce(compass);

            Assert.IsNull(thrown, "dropping one marker mustn't break the frame");
            Assert.AreEqual(0, markers.Count, "every dead marker dropped");
            yield return null;
        }
    }
}
