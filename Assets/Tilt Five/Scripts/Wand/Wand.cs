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

using System;
using System.Collections.Generic;
using UnityEngine;
using TiltFive.Logging;

namespace TiltFive
{
    /// <summary>
    /// Wand Settings encapsulates all configuration data used by the Wand's
    /// tracking runtime to compute the Wand Pose and apply it to the driven GameObject.
    /// </summary>
    [System.Serializable]
    public class WandSettings : TrackableSettings
    {
        public ControllerIndex controllerIndex;

        public GameObject GripPoint;
        public GameObject FingertipPoint;
        public GameObject AimPoint;

        // TODO: Think about some accessors for physical attributes of the wand (length, distance to tip, etc)?
    }

    /// <summary>
    /// The Wand API and runtime.
    /// </summary>
    public class Wand : Singleton<Wand>
    {
        #region Private Fields

        private Dictionary<ControllerIndex, WandCore> wandCores = new Dictionary<ControllerIndex, WandCore>()
        {
            { ControllerIndex.Primary, new WandCore() },
            { ControllerIndex.Secondary, new WandCore() }
        };

        #endregion


        #region Public Functions

        // Update is called once per frame
        public static void Update(WandSettings wandSettings, ScaleSettings scaleSettings, GameBoardSettings gameBoardSettings)
        {
            Instance.wandCores[wandSettings.controllerIndex].Update(wandSettings, scaleSettings, gameBoardSettings);
        }

        /// <summary>
        /// Gets the position of the wand in world space.
        /// </summary>
        /// <param name="controllerIndex"></param>
        /// <param name="controllerPosition"></param>
        /// <returns></returns>
        public static Vector3 GetPosition(
            ControllerIndex controllerIndex = ControllerIndex.Primary,
            ControllerPosition controllerPosition = ControllerPosition.Grip)
        {
            switch (controllerPosition)
            {
                case ControllerPosition.Fingertips:
                    return Instance.wandCores[controllerIndex].fingertipsPosition_UnityWorldSpace;
                case ControllerPosition.Aim:
                    return Instance.wandCores[controllerIndex].aimPosition_UnityWorldSpace;
                default:
                    return Instance.wandCores[controllerIndex].Position_UnityWorldSpace;
            }
        }

        /// <summary>
        /// Gets the rotation of the wand in world space.
        /// </summary>
        /// <param name="controllerIndex"></param>
        /// <returns></returns>
        public static Quaternion GetRotation(ControllerIndex controllerIndex = ControllerIndex.Primary)
        {
            return Instance.wandCores[controllerIndex].Rotation_UnityWorldSpace;
        }

        #endregion


        #region Private Classes

        /// <summary>
        /// Internal Wand core runtime.
        /// </summary>
        private class WandCore : TrackableCore<WandSettings>
        {
            /// <summary>
            /// The default position of the wand relative to the board.
            /// </summary>
            /// <remarks>
            /// The wand GameObject will snap back to this position if the glasses and/or wand are unavailable.
            /// </remarks>
            private static readonly Vector3 DEFAULT_WAND_POSITION_GAME_BOARD_SPACE = new Vector3(0f, 0.25f, -0.25f);
            /// <summary>
            /// A left/right offset to the default wand position, depending on handedness.
            /// </summary>
            private static readonly Vector3 DEFAULT_WAND_HANDEDNESS_OFFSET_GAME_BOARD_SPACE = new Vector3(0.125f, 0f, 0f);

            /// <summary>
            /// The default rotation of the wand relative to the board.
            /// </summary>
            /// <remarks>
            /// The wand GameObject will snap back to this rotation if the glasses are unavailable.
            /// If different behavior is desired in this scenario, a different camera should be used.
            /// </remarks>
            private static readonly Quaternion DEFAULT_WAND_ROTATION_GAME_BOARD_SPACE = Quaternion.Euler(new Vector3(-33f, 0f, 0f));

            private Vector3 fingertipsPosition_GameBoardSpace = DEFAULT_WAND_POSITION_GAME_BOARD_SPACE;
            private Vector3 aimPosition_GameBoardSpace = DEFAULT_WAND_POSITION_GAME_BOARD_SPACE;

            public Vector3 gripPosition_UnityWorldSpace => position_UnityWorldSpace;
            public Vector3 fingertipsPosition_UnityWorldSpace;
            public Vector3 aimPosition_UnityWorldSpace;

            public new void Update(WandSettings wandSettings, ScaleSettings scaleSettings, GameBoardSettings gameBoardSettings)
            {
                if (wandSettings == null)
                {
                    Log.Error("WandSettings configuration required for Wand tracking updates.");
                    return;
                }

                base.Update(wandSettings, scaleSettings, gameBoardSettings);

                fingertipsPosition_UnityWorldSpace =
                    GameBoardToWorldSpace(fingertipsPosition_GameBoardSpace, scaleSettings, gameBoardSettings);
                aimPosition_UnityWorldSpace
                    = GameBoardToWorldSpace(aimPosition_GameBoardSpace, scaleSettings, gameBoardSettings);
            }

            protected override Vector3 GetDefaultPositionGameBoardSpace(WandSettings settings)
            {
                Vector3 defaultPosition = DEFAULT_WAND_POSITION_GAME_BOARD_SPACE;

                defaultPosition += DEFAULT_WAND_HANDEDNESS_OFFSET_GAME_BOARD_SPACE
                    * (settings.controllerIndex == ControllerIndex.Primary ? 1f : -1f);

                return defaultPosition;
            }

            protected override Quaternion GetDefaultRotationGameBoardSpace(WandSettings settings)
            {
                return DEFAULT_WAND_ROTATION_GAME_BOARD_SPACE;
            }

            protected override bool GetTrackingAvailability(WandSettings settings)
            {
                return Display.GetGlassesAvailability() && Input.GetWandAvailability(settings.controllerIndex);
            }

            protected override bool TryGetPoseFromPlugin(out Vector3 position, out Quaternion rotation, WandSettings settings)
            {
                float[] gripPositionResult = new float[3]
                {
                    position_GameBoardSpace.x, position_GameBoardSpace.y, position_GameBoardSpace.z
                };
                float[] fingertipsPositionResult = new float[3]
                {
                    fingertipsPosition_GameBoardSpace.x, fingertipsPosition_GameBoardSpace.y, fingertipsPosition_GameBoardSpace.z
                };
                float[] aimPositionResult = new float[3]
                {
                    aimPosition_GameBoardSpace.x, aimPosition_GameBoardSpace.y, aimPosition_GameBoardSpace.z
                };
                float[] rotationResult = new float[4]
                {
                    rotation_GameBoardSpace.x, rotation_GameBoardSpace.y, rotation_GameBoardSpace.z, rotation_GameBoardSpace.w
                };

                int result = 1;

                try
                {
                    result = NativePlugin.GetControllerPose(rotationResult, gripPositionResult, fingertipsPositionResult, aimPositionResult, settings.controllerIndex);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }

                position = new Vector3(gripPositionResult[0], gripPositionResult[1], gripPositionResult[2]);
                fingertipsPosition_GameBoardSpace = new Vector3(fingertipsPositionResult[0], fingertipsPositionResult[1], fingertipsPositionResult[2]);
                aimPosition_GameBoardSpace = new Vector3(aimPositionResult[0], aimPositionResult[1], aimPositionResult[2]);
                rotation = new Quaternion(rotationResult[0], rotationResult[1], rotationResult[2], rotationResult[3]);

                return result == 0;
            }

            protected override void SetDrivenObjectTransform(WandSettings settings)
            {
                if(GameBoard.TryGetGameboardType(out var gameboardType) && gameboardType == GameboardType.GameboardType_None)
                {
                    // TODO: Implement default poses for wands when the glasses lose tracking.
                    return;
                }

                if(settings.GripPoint != null)
                {
                    settings.GripPoint.transform.position = gripPosition_UnityWorldSpace;
                    settings.GripPoint.transform.rotation = rotation_UnityWorldSpace;
                }

                if (settings.FingertipPoint != null)
                {
                    settings.FingertipPoint.transform.position = fingertipsPosition_UnityWorldSpace;
                    settings.FingertipPoint.transform.rotation = rotation_UnityWorldSpace;
                }

                if (settings.AimPoint != null)
                {
                    settings.AimPoint.transform.position = aimPosition_UnityWorldSpace;
                    settings.AimPoint.transform.rotation = rotation_UnityWorldSpace;
                }
            }
        }

        #endregion
    }

}