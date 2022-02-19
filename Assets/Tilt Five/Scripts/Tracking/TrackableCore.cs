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

using UnityEngine;
using TiltFive.Logging;

namespace TiltFive
{
    public abstract class TrackableCore<T> where T : TrackableSettings
    {
        /// <summary>
        /// The position of the trackable w.r.t. the game board reference frame.
        /// </summary>
        protected Vector3 position_GameBoardSpace = Vector3.zero;
        public Vector3 Position_GameBoardSpace { get => position_GameBoardSpace; }

        /// <summary>
        /// The rotation from the game board reference frame to the trackable's reference frame.
        /// </summary>
        protected Quaternion rotation_GameBoardSpace = Quaternion.identity;
        public Quaternion Rotation_GameBoardSpace { get => rotation_GameBoardSpace; }

        /// <summary>
        /// The position of the trackable in Unity world space.
        /// </summary>
        protected Vector3 position_UnityWorldSpace = Vector3.zero;
        public Vector3 Position_UnityWorldSpace { get => position_UnityWorldSpace; }

        /// <summary>
        /// The rotation of the trackable in Unity world space.
        /// </summary>
        protected Quaternion rotation_UnityWorldSpace = Quaternion.identity;
        public Quaternion Rotation_UnityWorldSpace { get => rotation_UnityWorldSpace; }

        /// <summary>
        /// The position of the game board reference frame w.r.t. the Unity
        /// world-space reference frame.
        /// </summary>
        protected Vector3 gameBoardPosition_UnityWorldSpace = Vector3.zero;

        /// <summary>
        /// The rotation taking points from the Unity world-space reference
        /// frame to the game board reference frame.
        /// </summary>
        protected Quaternion gameBoardRotation_UnityWorldSpace = Quaternion.identity;

        // Update is called once per frame
        protected void Update(T settings, ScaleSettings scaleSettings, GameBoardSettings gameBoardSettings)
        {
            if(settings == null)
            {
                Log.Error("TrackableSettings configuration required for tracking updates.");
                return;
            }

            // Get the game board pose.
            gameBoardPosition_UnityWorldSpace = gameBoardSettings.gameBoardCenter;
            gameBoardRotation_UnityWorldSpace = Quaternion.Inverse(gameBoardSettings.currentGameBoard.rotation);

            // Get the latest pose w.r.t. the game board.
            position_GameBoardSpace = GetDefaultPositionGameBoardSpace(settings);
            rotation_GameBoardSpace = GetDefaultRotationGameBoardSpace(settings);

            if(GetTrackingAvailability(settings))
            {
                TryGetPoseFromPlugin(out position_GameBoardSpace, out rotation_GameBoardSpace, settings);
            }

            position_UnityWorldSpace = GameBoardToWorldSpace(position_GameBoardSpace, scaleSettings, gameBoardSettings);
            rotation_UnityWorldSpace = GameBoardToWorldSpace(rotation_GameBoardSpace, gameBoardSettings);

            SetDrivenObjectTransform(settings);
        }

        protected static Vector3 GameBoardToWorldSpace(Vector3 position,
            ScaleSettings scaleSettings, GameBoardSettings gameBoardSettings)
        {
            float scaleToUGBL_UWRLD = scaleSettings.physicalMetersPerWorldSpaceUnit * gameBoardSettings.gameBoardScale;
            if (scaleToUGBL_UWRLD <= 0)
            {
                Log.Error("Division by zero error: Content Scale and Game Board scale must be positive non-zero values.");
                scaleToUGBL_UWRLD = Mathf.Max(scaleToUGBL_UWRLD, float.Epsilon);
            }
            float scaleToUWRLD_UGBL = 1.0f / scaleToUGBL_UWRLD;

            return gameBoardSettings.currentGameBoard.rotation *
                (scaleToUWRLD_UGBL * position) + gameBoardSettings.gameBoardCenter;
        }

        protected static Quaternion GameBoardToWorldSpace(Quaternion rotation, GameBoardSettings gameBoardSettings)
        {
            Quaternion rotToUGLS_UWRLD = rotation * Quaternion.Inverse(gameBoardSettings.currentGameBoard.rotation);
            return Quaternion.Inverse(rotToUGLS_UWRLD);
        }

        /// <summary>
        /// Gets the default position of the tracked object.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected abstract Vector3 GetDefaultPositionGameBoardSpace(T settings);

        /// <summary>
        /// Gets the default rotation of the tracked object.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected abstract Quaternion GetDefaultRotationGameBoardSpace(T settings);

        /// <summary>
        /// Checks if the native plugin can get a new pose for the tracked object.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected abstract bool GetTrackingAvailability(T settings);

        /// <summary>
        /// Gets the latest pose for the tracked object from the native plugin.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected abstract bool TryGetPoseFromPlugin(out Vector3 position, out Quaternion rotation, T settings);

        /// <summary>
        /// Sets the pose of the object(s) being driven by TrackableCore.
        /// </summary>
        /// <param name="settings"></param>
        protected abstract void SetDrivenObjectTransform(T settings);
    }
}