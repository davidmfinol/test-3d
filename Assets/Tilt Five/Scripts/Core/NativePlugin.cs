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
using System.Runtime.InteropServices;

namespace TiltFive
{
    public class NativePlugin
    {

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        public const string PLUGIN_LIBRARY = @"__Internal";
#else
        public const string PLUGIN_LIBRARY = @"TiltFiveUnity";
#endif

        // Init
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int SetApplicationInfo(
            System.IntPtr pAppName,     // Pointer to a byte array containing UTF-8 string data
            System.IntPtr pAppId,       // Pointer to a byte array containing UTF-8 string data
            System.IntPtr pAppVersion); // Pointer to a byte array containing UTF-8 string data

        // Glasses Availability
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int RefreshGlassesAvailable();

        // Head Pose
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetGlassesPose(
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] float[] rotToGLS_WRLD,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] posGLS_WRLD);

        // Gameboard type
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetGameboardType(
            [MarshalAs(UnmanagedType.I4)] ref GameboardType gameboardType);

        // Gameboard dimensions
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetGameboardDimensions(
            [MarshalAs(UnmanagedType.I4)] GameboardType gameboardType,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 2)] ref float[] playableSpaceInMeters,
            ref float borderWidthInMeters);

        // Wand Availability
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetWandAvailability(
            ref bool wandAvailable,
            [MarshalAs(UnmanagedType.I4)] ControllerIndex wandTarget);

        // Scan for Wands
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int ScanForWands();

        // Swap Wand Handedness
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int SwapWandHandedness();

        // Wand Controls State
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetControllerState(
            [MarshalAs(UnmanagedType.I4)] ControllerIndex wandTarget,
            ref UInt32 buttons,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 2)] float[] stick,
            ref float trigger,
            ref Int64 timestamp);

        // Wand Pose
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetControllerPose(
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] float[] rotToGLS_WRLD,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] gripPosGLS_WRLD,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] fingertipsPosGLS_WRLD,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] aimPosGLS_WRLD,
            [MarshalAs(UnmanagedType.I4)] ControllerIndex controllerIndex);

        [DllImport(PLUGIN_LIBRARY)]
        public static extern int SetRumbleMotor(uint motor, float intensity);

        // Submit Render Textures
        [DllImport(PLUGIN_LIBRARY)]
        public static extern int QueueStereoImages(
                System.IntPtr leftEyeTextureHandle,
                System.IntPtr rightEyeTextureHandle,
                ushort texWidth_PIX,
                ushort texHeight_PIX,
                bool isSrgb,
                float startX_VCI,
                float startY_VCI,
                float width_VCI,
                float height_VCI,
                [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] float[] rotToULVC_UGBL,
                [MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] posULVC_UGBL,
                [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] float[] rotToURVC_UGBL,
                [MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] posURVC_UGBL);

        [DllImport(PLUGIN_LIBRARY)]
        public static extern IntPtr GetSendFrameCallback();

        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetMaxDisplayDimensions(
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 2)] int[] displayDimensions);

        [DllImport(PLUGIN_LIBRARY)]
        public static extern int GetGlassesIPD(ref float glassesIPD);

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport ("__Internal")]
        public static extern void RegisterPlugin();
#endif

    }

    /// <summary>
    /// Since wands are all physically identical (they have no "handedness"), it doesn't make sense to address them using "left" or "right".
    /// Instead we use hand dominance, and allow applications to swap the dominant and offhand wand according to the user's preference.
    /// </summary>
    public enum ControllerIndex : Int32
    {
        /// <summary>
        /// The wand held in the player's dominant hand.
        /// </summary>
        Primary = 0,

        /// <summary>
        /// The wand held in the player's non-dominant hand.
        /// </summary>
        Secondary = 1
    }

    public enum GameboardType : Int32
    {
        /// <summary>
        /// No Gameboard at all.
        /// </summary>
        /// <remarks>
        /// If the glasses pose is in respect to GameboardType.GameboardType_None
        /// </remarks>
        GameboardType_None = 0,

        /// <summary>
        /// The LE Gameboard.
        /// </summary>
        GameboardType_LE = 1,

        /// <summary>
        /// The XE Gameboard.
        /// </summary>
        GameboardType_XE = 2
    }

    public enum ControllerPosition : Int32
    {
        /// <summary>
        /// The center of the wand handle.
        /// </summary>
        Grip = 0,

        /// <summary>
        /// The typical resting position of the player's fingertips, near the wand joystick and trigger.
        /// </summary>
        Fingertips = 1,

        /// <summary>
        /// The tip of the wand.
        /// </summary>
        Aim = 2
    }
}
