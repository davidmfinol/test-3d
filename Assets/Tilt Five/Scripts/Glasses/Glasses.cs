/*
 * Copyright (C) 2020 Tilt Five, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using UnityEngine;

using TiltFive;
using TiltFive.Logging;

namespace TiltFive
{
    /// <summary>
    /// The Glasses API and runtime.
    /// </summary>
    public sealed class Glasses : Singleton<Glasses>
    {

        #region Private Fields

        /// <summary>
        /// The glasses core runtime.
        /// </summary>
        private GlassesCore glassesCore = new GlassesCore();

        #endregion


        #region public Enums

        public enum AREyes
        {
            EYE_LEFT = 0,
            EYE_RIGHT,
            EYE_MAX,
        }

        #endregion


        #region Public Fields

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:TiltFive.Glasses"/> is updated.
        /// </summary>
        /// <value><c>true</c> if updated; otherwise, <c>false</c>.</value>
        public static bool updated => Instance.glassesCore.TrackingUpdated;
        /// <summary>
        /// Gets a value indicating whether this <see cref="T:TiltFive.Glasses"/> is configured.
        /// </summary>
        /// <value><c>true</c> if configured; otherwise, <c>false</c>.</value>
        public static bool configured => Instance.glassesCore.configured;
        /// <summary>
        /// Gets the head pose position.
        /// </summary>
        /// <value>The position.</value>
        public static Vector3 position => Instance.glassesCore.Position_UnityWorldSpace;
        /// <summary>
        /// Gets the head pose rotation.
        /// </summary>
        /// <value>The rotation.</value>
        public static Quaternion rotation => Instance.glassesCore.Rotation_UnityWorldSpace;
        /// <summary>
        /// Gets the head orientation's forward vector.
        /// </summary>
        /// <value>The forward vector.</value>
        public static Vector3 forward => rotation * Vector3.forward;
        /// <summary>
        /// Gets the head orientation's right vector.
        /// </summary>
        /// <value>The right vector.</value>
        public static Vector3 right => rotation * Vector3.right;
        /// <summary>
        /// Gets the head orientation's up vector.
        /// </summary>
        /// <value>The up vector.</value>
        public static Vector3 up => rotation * Vector3.up;

        /// <summary>
        /// Gets the left eye position.
        /// </summary>
        /// <value>The left eye position.</value>
        public static Vector3 leftEyePosition => Instance.glassesCore.eyePositions[AREyes.EYE_LEFT];
        /// <summary>
        /// Gets the right eye position.
        /// </summary>
        /// <value>The right eye position.</value>
        public static Vector3 rightEyePosition => Instance.glassesCore.eyePositions[AREyes.EYE_RIGHT];

        /// <summary>
        /// Indicates whether the glasses are plugged in and functioning.
        /// </summary>
        public static bool glassesAvailable {get; private set;}

        #endregion Public Fields


        #region Public Functions

        /// <summary>
        /// Returns a boolean indication that the head pose was successfully
        /// updated.
        /// </summary>
        /// <returns><c>true</c>, if the head pose was updated, <c>false</c> otherwise.</returns>
        public static bool headPoseUpdated() { return Instance.glassesCore.TrackingUpdated; }

        /// <summary>
        /// Reset this <see cref="T:TiltFive.Glasses"/>.
        /// </summary>
        /// <param name="glassesSettings">Glasses settings for configuring the instance.</param>
        public static void Reset(GlassesSettings glassesSettings)
        {
            Instance.glassesCore.Reset(glassesSettings);
        }

        /// <summary>
        /// Validates the specified glassesSettings with the current instance.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the glasses core is valid with the given settings,
        ///     <c>false</c> otherwise.
        /// </returns>
        /// <param name="glassesSettings">Glasses settings.</param>
        public static bool Validate(GlassesSettings glassesSettings)
        {
            return Instance.glassesCore.Validate(glassesSettings);
        }

        /// <summary>
        /// Updates this <see cref="T:TiltFive.Glasses"/>.
        /// </summary>
        /// <param name="glassesSettings">Glasses settings for the update.</param>
        public static void Update(GlassesSettings glassesSettings, ScaleSettings scaleSettings, GameBoardSettings gameBoardSettings)
        {
            Instance.glassesCore.Update(glassesSettings, scaleSettings, gameBoardSettings);
        }

        #endregion Public Functions

        /// <summary>
        /// Internal Glasses core runtime.
        /// </summary>
        private class GlassesCore : TrackableCore<GlassesSettings>
        {

            /// <summary>
            /// Configuration ready indicator.
            /// </summary>
            public bool configured = false;

            public Dictionary<AREyes, Vector3> eyePositions = new Dictionary<AREyes, Vector3>()
            {
                { AREyes.EYE_LEFT, new Vector3() },
                { AREyes.EYE_RIGHT, new Vector3() }
            };

            public Dictionary<AREyes, Quaternion> eyeRotations = new Dictionary<AREyes, Quaternion>()
            {
                { AREyes.EYE_LEFT, new Quaternion() },
                { AREyes.EYE_RIGHT, new Quaternion() }
            };

            /// <summary>
            /// The default position of the glasses relative to the board.
            /// </summary>
            /// <remarks>
            /// The glasses camera will snap back to this position if the glasses are unavailable.
            /// If different behavior is desired in this scenario, a different camera should be used.
            /// </remarks>
            private readonly Vector3 DEFAULT_GLASSES_POSITION_GAME_BOARD_SPACE = new Vector3(0f, 0.5f, -0.5f);

            /// <summary>
            /// The default rotation of the glasses relative to the board.
            /// </summary>
            /// <remarks>
            /// The glasses camera will snap back to this rotation if the glasses are unavailable.
            /// If different behavior is desired in this scenario, a different camera should be used.
            /// </remarks>
            private readonly Quaternion DEFAULT_GLASSES_ROTATION_GAME_BOARD_SPACE = Quaternion.Euler(new Vector3(-45f, 0f, 0f));

            /// <summary>
            /// Gets a value indicating whether this <see cref="T:TiltFive.Glasses.GlassesCore"/> tracking was successfully updated.
            /// </summary>
            /// <value><c>true</c> if tracking updated; otherwise, <c>false</c>.</value>
            public bool TrackingUpdated { get; private set; } = false;

            /// <summary>
            /// The split stereo camera implementation used in lieu of XRSettings.
            /// </summary>
            protected SplitStereoCamera splitStereoCamera = null;

            /// <summary>
            /// Reset this <see cref="T:TiltFive.Glasses.GlassesCore"/>
            /// </summary>
            /// <param name="glassesSettings">Glasses settings for configuring the instance.</param>
            public void Reset(GlassesSettings glassesSettings)
            {
                configured = false;

                if (null == glassesSettings.headPoseCamera)
                {
                    Log.Error($"Required Camera assignment missing from { GetType() }.");
                    return;
                }

                if(glassesSettings.headPoseCamera.fieldOfView != glassesSettings.fieldOfView)
                {
                    glassesSettings.headPoseCamera.fieldOfView = glassesSettings.fieldOfView;
                }

#if UNITY_EDITOR
                if (glassesSettings.tiltFiveXR)
                {
#endif
                    //if the splitScreenCamera does not exist already.
                    if (null == splitStereoCamera)
                    {
                        //get the head pose camera's GameObject
                        GameObject cameraObject = glassesSettings.headPoseCamera.gameObject;

                        //Check whether it is set up as a SplitScreenCamera, and if not:
                        if (!cameraObject.TryGetComponent<SplitStereoCamera>(out splitStereoCamera))
                        {
                            // Add it ourselves. The OnAwake call will create & configure
                            // the eye cameras to render with. it will also use theCamera
                            // as the source.
                            splitStereoCamera = cameraObject.AddComponent<SplitStereoCamera>();
                        }
                    }
#if UNITY_EDITOR
                }
#endif //UNITY_EDITOR

                configured = true;
            }

            /// <summary>
            /// Tests this <see cref="T:TiltFive.Glasses.GlassesCore"/> for validity
            /// with the paramterized <see cref="T:TiltFive.Glasses.GlassesSettings"/>
            /// </summary>
            /// <returns><c>true</c>, if valid, <c>false</c> otherwise.</returns>
            /// <param name="glassesSettings">Glasses settings.</param>
            public bool Validate(GlassesSettings glassesSettings)
            {
                bool valid = true;
                valid &= (glassesSettings.headPoseCamera == splitStereoCamera.headPoseCamera);
                valid &= (glassesSettings.headPoseCamera.fieldOfView == glassesSettings.fieldOfView);
                return valid;
            }

            /// <summary>
            /// Updates this <see cref="T:TiltFive.Glasses.GlassesCore"/>
            /// </summary>
            /// <param name="glassesSettings">Glasses settings for the update.</param>
            public new void Update(GlassesSettings glassesSettings, ScaleSettings scaleSettings, GameBoardSettings gameBoardSettings)
            {
                TrackingUpdated = false;

                if (null == glassesSettings)
                {
                    Log.Error("GlassesSettings configuration required for Glasses tracking Update.");
                    return;
                }

                if (null == splitStereoCamera)
                {
                    Log.Error("Stereo camera(s) missing from Glasses - aborting Update.");
                    return;
                }

                if (glassesSettings.headPoseCamera != splitStereoCamera.headPoseCamera)
                {
                    Log.Warn("Found mismatched Cameras in GlassesCore Update - should call Reset.");
                    return;
                }

                // Check whether the glasses are plugged in and available.
                glassesAvailable = GetTrackingAvailability(glassesSettings);
                splitStereoCamera.enabled = glassesAvailable;
                splitStereoCamera.glassesMirrorMode = glassesSettings.glassesMirrorMode;

                if (glassesSettings.headPoseCamera == null)
                {
                    Log.Error("Head pose camera required for Glasses tracking.");
                    return;
                }
                else
                {
                    // Obtain the latest glasses pose.
                    base.Update(glassesSettings, scaleSettings, gameBoardSettings);
                }

                // Get the glasses pose in Unity world-space.
                float scaleToUGBL_UWRLD = scaleSettings.physicalMetersPerWorldSpaceUnit * gameBoardSettings.gameBoardScale;
                if (scaleToUGBL_UWRLD <= 0)
                {
                    Log.Error("Division by zero error: Content Scale and Game Board scale must be positive non-zero values.");
                    scaleToUGBL_UWRLD = Mathf.Max(scaleToUGBL_UWRLD, float.Epsilon);
                }

                float scaleToUWRLD_UGBL = 1.0f / scaleToUGBL_UWRLD;

                // Set the game board transform on the SplitStereoCamera.
                splitStereoCamera.posUGBL_UWRLD = gameBoardPosition_UnityWorldSpace;
                splitStereoCamera.rotToUGBL_UWRLD = gameBoardRotation_UnityWorldSpace;
                splitStereoCamera.scaleToUGBL_UWRLD = scaleToUGBL_UWRLD;

                // TODO: Revisit native XR support.

                // NOTE: We do this because "Mock HMD" in UNITY_2017_0_2_OR_NEWER
                // the fieldOfView is locked to 111.96 degrees (Vive emulation),
                // so setting custom projection matrices is broken. If Unity
                // opens the API to custom settings, we can go back to native XR
                // support.

                // Manual split screen 'new glasses' until the day Unity lets
                // me override their Mock HMD settings.

                Transform headPose = glassesSettings.headPoseCamera.transform;

                // compute half ipd translation
                float ipd_UGBL = GlassesSettings.DEFAULT_IPD_UGBL;
                if(!Display.GetGlassesIPD(ref ipd_UGBL) && glassesAvailable)
                {
                    Log.Error("Failed to obtain Glasses IPD");
                }
                float ipd_UWRLD = scaleToUWRLD_UGBL * ipd_UGBL;
                Vector3 eyeOffset = (headPose.right.normalized * (ipd_UWRLD * 0.5f));

                // set the left eye camera offset from the head by the half ipd amount (-)
                eyePositions[AREyes.EYE_LEFT] = headPose.position - eyeOffset;
                eyeRotations[AREyes.EYE_LEFT] = headPose.rotation;

                // set the right eye camera offset from the head by the half ipd amount (+)
                eyePositions[AREyes.EYE_RIGHT] = headPose.position + eyeOffset;
                eyeRotations[AREyes.EYE_RIGHT] = headPose.rotation;

                Camera leftEyeCamera = splitStereoCamera.leftEyeCamera;
                if (null != leftEyeCamera)
                {
                    GameObject leftEye = leftEyeCamera.gameObject;
                    leftEye.transform.position = eyePositions[AREyes.EYE_LEFT];
                    leftEye.transform.rotation = eyeRotations[AREyes.EYE_LEFT];

                    //make sure projection fields are synchronized to the head camera.
                    leftEyeCamera.nearClipPlane = glassesSettings.headPoseCamera.nearClipPlane;
                    leftEyeCamera.farClipPlane = glassesSettings.headPoseCamera.farClipPlane;
                    leftEyeCamera.fieldOfView = glassesSettings.headPoseCamera.fieldOfView;
                }

                Camera rightEyeCamera = splitStereoCamera.rightEyeCamera;
                if (null != rightEyeCamera)
                {
                    GameObject rightEye = rightEyeCamera.gameObject;
                    rightEye.transform.position = eyePositions[AREyes.EYE_RIGHT];
                    rightEye.transform.rotation = eyeRotations[AREyes.EYE_RIGHT];

                    //make sure projection fields are synchronized to the head camera.
                    rightEyeCamera.nearClipPlane = glassesSettings.headPoseCamera.nearClipPlane;
                    rightEyeCamera.farClipPlane = glassesSettings.headPoseCamera.farClipPlane;
                    rightEyeCamera.fieldOfView = glassesSettings.headPoseCamera.fieldOfView;
                }
            }

            protected override Vector3 GetDefaultPositionGameBoardSpace(GlassesSettings settings)
            {
                return DEFAULT_GLASSES_POSITION_GAME_BOARD_SPACE;
            }

            protected override Quaternion GetDefaultRotationGameBoardSpace(GlassesSettings settings)
            {
                return DEFAULT_GLASSES_ROTATION_GAME_BOARD_SPACE;
            }

            protected override bool GetTrackingAvailability(GlassesSettings settings)
            {
                // TODO: Think about checking for GameboardType.GameboardType_None here.
                // Currently this would prevent SetDrivenObjectTransform() from being called,
                // which would prevent the preview pose from being used,
                // so some untangling would be necessary.
                // Perhaps this function should be renamed to "GetDeviceAvailability()"?
                // Maybe a new function in TrackableCore like "HandleTrackingLost()" that would
                // be called instead of SetDrivenObjectTransform() should be added?
                // In any case, some untangling should happen.
                return Display.GetGlassesAvailability();
            }

            protected override bool TryGetPoseFromPlugin(out Vector3 position, out Quaternion rotation, GlassesSettings settings)
            {
                float[] positionResult = new float[3]
                {
                    position_GameBoardSpace.x, position_GameBoardSpace.y, position_GameBoardSpace.z
                };
                float[] rotationResult = new float[4]
                {
                    rotation_GameBoardSpace.x, rotation_GameBoardSpace.y, rotation_GameBoardSpace.z, rotation_GameBoardSpace.w
                };

                int result = 1;
                try
                {
                    result = NativePlugin.GetGlassesPose(rotationResult, positionResult);
                }
                catch (System.Exception e)
                {
                    Log.Error(e.Message);
                }

                position = new Vector3(positionResult[0], positionResult[1], positionResult[2]);
                rotation = new Quaternion(rotationResult[0], rotationResult[1], rotationResult[2], rotationResult[3]);

                return result == 0;
            }

            protected override void SetDrivenObjectTransform(GlassesSettings settings)
            {
                // Handle a loss of head tracking if necessary.
                if(GameBoard.TryGetGameboardType(out var gameboardType)
                    && gameboardType == GameboardType.GameboardType_None)
                {
                    // Use the preview pose instead of the one returned by the service.
                    if(settings.usePreviewPose)
                    {
                        settings.headPoseCamera?.transform.SetPositionAndRotation(
                            settings.previewPose.position,
                            settings.previewPose.rotation);
                        return;
                    }
                    // Otherwise do nothing and let the developer control the head pose camera themselves.
                    // It will be up to them to let go once head tracking kicks in again.
                    return;
                }

                settings.headPoseCamera?.transform.SetPositionAndRotation(
                    position_UnityWorldSpace,
                    rotation_UnityWorldSpace);
            }
        }
    }
}
