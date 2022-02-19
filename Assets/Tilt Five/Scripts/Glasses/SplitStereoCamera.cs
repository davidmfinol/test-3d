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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TiltFive.Logging;

#if TILT_FIVE_SRP
using UnityEngine.Rendering;
#endif

using AREyes = TiltFive.Glasses.AREyes;

namespace TiltFive
{

    /// <summary>
    /// Display settings constants.
    /// </summary>
    [System.Serializable]
    public class DisplaySettings
    {
        private static DisplaySettings instance;
        private static DisplaySettings Instance
        {
            get
            {
                if(instance == null)
                {
                    instance = new DisplaySettings();
                }
                return instance;
            }
            set => instance = value;
        }

        private DisplaySettings()
        {
            if(!Display.GetDisplayDimensions(ref defaultDimensions))
            {
                Log.Warn("Could not retrieve display settings from the plugin.");
            }
        }

        /// <summary> The display width for a single eye. </summary>
        public static int monoWidth => (stereoWidth / 2);
        /// <summary> The display width for two eyes. </summary>
        public static int stereoWidth => Instance.defaultDimensions.x;
        /// <summary> The display height. </summary>
        public static int height => Instance.defaultDimensions.y;
        /// <summary> The display aspect ratio. </summary>
        public static float monoWidthToHeightRatio => (float) monoWidth / height;
        /// <summary> The double-width display aspect ratio. </summary>
        public static float stereoWidthToHeightRatio => (float) stereoWidth / height;
        /// <summary> The depth buffer's precision. </summary>
        public const int depthBuffer = 24;

        // Provide a texture format compatible with the glasses.
        public const RenderTextureFormat defaultTextureFormat = RenderTextureFormat.ARGB32;

        // Provide a hardcoded default resolution if the plugin is somehow unavailable.
        private readonly Vector2Int defaultDimensions = new Vector2Int(2432, 768);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public partial class SplitStereoCamera : MonoBehaviour
    {

        /// <summary> Name assigned to any dynamically created (missing) head pose camera. </summary>
        private const string HEAD_CAMERA_NAME = "Head Camera";
        /// <summary> The head pose (Main) camera. </summary>
        private Camera theHeadPoseCamera = null;
        /// <summary> The head pose Camera property. </summary>
        public Camera headPoseCamera { get { return theHeadPoseCamera; } }
        /// <summary> The head pose GameObject property. </summary>
        public GameObject headPose { get { return theHeadPoseCamera.gameObject; } }

        /// <summary> The name assigned to the dynamically created camera used for rendering the left eye. </summary>
        private const string LEFT_EYE_CAMERA_NAME = "Left Eye Camera";
        /// <summary> The left eye camera GameObject. </summary>
        private GameObject leftEye;
        /// <summary> The left eye Camera property. </summary>
        public Camera leftEyeCamera { get { return eyeCameras[AREyes.EYE_LEFT]; } }

        /// <summary> The name assigned to the dynamically created camera used for rendering the right eye. </summary>
        private const string RIGHT_EYE_CAMERA_NAME = "Right Eye Camera";
        /// <summary> The right eye camera GameObject. </summary>
        private GameObject rightEye;
        /// <summary> The right eye Camera property. </summary>
        public Camera rightEyeCamera { get { return eyeCameras[AREyes.EYE_RIGHT]; } }

        /// <summary> In-editor toggle for displaying the eye cameras in the runtime Hierarchy. </summary>
        public bool showCameras = true;
        /// <summary> The Camera objects. </summary>
        private Dictionary<AREyes, Camera> eyeCameras = new Dictionary<AREyes, Camera>()
        {
            { AREyes.EYE_LEFT, null },
            { AREyes.EYE_RIGHT, null }
        };

        /// <summary>
        /// The position of the game board reference frame w.r.t. the Unity
        /// world-space reference frame.
        /// </summary>
        public Vector3 posUGBL_UWRLD = Vector3.zero;

        /// <summary>
        /// The rotation taking points from the Unity world-space reference
        /// frame to the game board reference frame.
        /// </summary>
        public Quaternion rotToUGBL_UWRLD = Quaternion.identity;

        /// <summary>
        /// The uniform scale factor that takes points from the Unity
        /// world-space to the game board reference frame.
        /// </summary>
        public float scaleToUGBL_UWRLD = 1.0f;

        /// <summary> The name of the custom shader that blits the rendertextures to the backbuffer. </summary>
        private const string SHADER_DISPLAY_BLIT = "Tilt Five/Simple Blend Shader";
        /// <summary> The Material used to store/reference the shader. </summary>
        private Material displayBlitShader;

        private System.IntPtr leftTexHandle;
        private System.IntPtr rightTexHandle;

        [HideInInspector]
        public GlassesMirrorMode glassesMirrorMode = GlassesMirrorMode.LeftEye;
        private RenderTexture singlePreviewTex;
        private RenderTexture doublePreviewTex;

#if TILT_FIVE_SRP
        private CommandBuffer commandBuffer;
#endif

        /// <summary> The Cameras' field of view property. </summary>
        public float fieldOfView
        {
            get { return headPoseCamera.fieldOfView; }
            set { rightEyeCamera.fieldOfView = leftEyeCamera.fieldOfView = headPoseCamera.fieldOfView = value; }
        }

        /// <summary> The Cameras' near clip plane property. </summary>
        public float nearClipPlane
        {
            get { return headPoseCamera.nearClipPlane; }
            set { rightEyeCamera.nearClipPlane = leftEyeCamera.nearClipPlane = headPoseCamera.nearClipPlane = value; }
        }

        /// <summary> The Cameras' far clip plane property. </summary>
        public float farClipPlane
        {
            get { return headPoseCamera.farClipPlane; }
            set { rightEyeCamera.farClipPlane = leftEyeCamera.farClipPlane = headPoseCamera.farClipPlane = value; }
        }

        /// <summary> The Cameras' aspect ratio property. </summary>
        public float aspectRatio
        {
            get { return headPoseCamera.aspect; }
            set
            {
                headPoseCamera.aspect = value;
            }
        }

        /// <summary>
        /// Awake this instance.
        /// </summary>
        void Awake()
        {
#if TILT_FIVE_SRP
            commandBuffer = new CommandBuffer() { name = "Onscreen Preview" };
#endif

#if UNITY_EDITOR
            // if we are initializing this, then we need to make sure we turn off XR
            // we need to do it before we start mucking with Cameras or else we get
            // warnings about XREyeTextures.
            if (UnityEngine.XR.XRSettings.enabled)
            {
                Log.Warn("XRSettings.enabled=true loadDeviceName={0}. Setting to none and disabling in {1}.", UnityEngine.XR.XRSettings.loadedDeviceName, GetType());
                StartCoroutine(LoadUnityXRSDK("none", false));
            }
#endif


            // try to get the head camera from the GameObject
            // in RT mode, the Camera has to be the same object as the stero camera script.
            if (!this.TryGetComponent<Camera>(out theHeadPoseCamera))
            {
                //Create one on the GameObject
                theHeadPoseCamera = gameObject.AddComponent<Camera>();
                headPoseCamera.name = HEAD_CAMERA_NAME;
                Log.Warn("Runtime AddComponent<Camera> to GameObject.name={0}", HEAD_CAMERA_NAME);
            }

            // Our manual Split Stereo won't work with HDR enabled during setup
            // because (of course) Unity breaks viewport settings in this case.
            bool allowHDR = theHeadPoseCamera.allowHDR;
            theHeadPoseCamera.allowHDR = false;

            // For this mode, we need the headPose Camera to be enabled, as it is the
            // primary Camera for blitting to the backbuffer.
            headPoseCamera.enabled = true;

            leftEye = GameObject.Find(LEFT_EYE_CAMERA_NAME);
            if (null != leftEye)
            {
                Destroy(leftEye);
                leftEye = null;
                Log.Warn("Runtime replacement of Scene's pre-existing GameObject.name={0}", LEFT_EYE_CAMERA_NAME);
            }

            leftEye = new GameObject(LEFT_EYE_CAMERA_NAME);
            leftEye.transform.parent = transform;
            eyeCameras[AREyes.EYE_LEFT] = leftEye.AddComponent<Camera>();

            leftEyeCamera.CopyFrom(headPoseCamera);

            //now the right eye Camera object
            rightEye = GameObject.Find(RIGHT_EYE_CAMERA_NAME);
            if (null != rightEye)
            {
                Destroy(rightEye);
                rightEye = null;
                Log.Warn("Runtime replacement of Scene's pre-existing GameObject.name={0}", RIGHT_EYE_CAMERA_NAME);
            }

            rightEye = new GameObject(RIGHT_EYE_CAMERA_NAME);
            rightEye.transform.parent = transform;
            eyeCameras[AREyes.EYE_RIGHT] = rightEye.AddComponent<Camera>();

            rightEyeCamera.CopyFrom(headPoseCamera);

            //create the left eye camera's render texture
            RenderTexture leftTex = new RenderTexture(
                DisplaySettings.monoWidth,
                DisplaySettings.height,
                DisplaySettings.depthBuffer,
                DisplaySettings.defaultTextureFormat);
            if (leftEyeCamera.allowMSAA && QualitySettings.antiAliasing > 1)
            {
                leftTex.antiAliasing = QualitySettings.antiAliasing;
            }

            leftEyeCamera.targetTexture = leftTex;
            leftEyeCamera.depth = headPoseCamera.depth - 1;

            //create the right eye camera's render texture
            RenderTexture rightTex = new RenderTexture(DisplaySettings.monoWidth,
                DisplaySettings.height,
                DisplaySettings.depthBuffer,
                DisplaySettings.defaultTextureFormat);
            if (rightEyeCamera.allowMSAA && QualitySettings.antiAliasing > 1)
            {
                rightTex.antiAliasing = QualitySettings.antiAliasing;
            }

            rightEyeCamera.targetTexture = rightTex;
            rightEyeCamera.depth = headPoseCamera.depth - 1;

            singlePreviewTex = new RenderTexture(
                DisplaySettings.monoWidth,
                DisplaySettings.height,
                DisplaySettings.depthBuffer,
                DisplaySettings.defaultTextureFormat);
            doublePreviewTex = new RenderTexture(
                DisplaySettings.stereoWidth,
                DisplaySettings.height,
                DisplaySettings.depthBuffer,
                DisplaySettings.defaultTextureFormat);

            // Load the blitting shader to copy the the left & right render textures
            // into the backbuffer
            displayBlitShader = new Material(Shader.Find(SHADER_DISPLAY_BLIT));
            // Did we find it?
            if (null == displayBlitShader)
            {
                Log.Error("Failed to load Shader '{0}'", SHADER_DISPLAY_BLIT);
            }

            theHeadPoseCamera.allowHDR = allowHDR; //restore HDR setting

            SyncFields(headPoseCamera);
            SyncTransform();
            showHideCameras();
        }

        /// <summary>
        /// Sets Unity's XR SDK to the input parameters. This should NEVER run.
        /// </summary>
        /// <returns>On completion.</returns>
        /// <param name="newDevice">The XR device name to load.</param>
        /// <param name="useXr">If set to <c>true</c>, XRSettings are enabled. Otherwise, false.</param>
        IEnumerator LoadUnityXRSDK(string newDevice, bool useXr)
        {
            if (System.String.Compare(UnityEngine.XR.XRSettings.loadedDeviceName, newDevice, true) != 0)
            {
                UnityEngine.XR.XRSettings.LoadDeviceByName(newDevice);
                yield return null;
                UnityEngine.XR.XRSettings.enabled = useXr;
                yield return new WaitForEndOfFrame();

                Log.Debug("loadedDeviceName={0} and XRSettings.enabled={1}",
                    UnityEngine.XR.XRSettings.loadedDeviceName,
                    UnityEngine.XR.XRSettings.enabled);
            }
        }

        /// <summary>
        /// EDITOR-ONLY: Syncs the eye Cameras' transform to the Head Pose
        /// when tracking is not available.
        /// </summary>
        void SyncTransform()
        {

#if UNITY_EDITOR
            // We move the eye Cameras in the Editor to emulate head pose and eye movement.
            // In builds, we only set the camera transforms with Glasses tracking data.

            if (null == headPoseCamera)
                return;

            if (!Glasses.updated)
            {
                GameObject pose = headPose;
                // left eye copy and adjust
                leftEye.transform.position = pose.transform.position;
                leftEye.transform.localPosition = pose.transform.localPosition;
                leftEye.transform.rotation = pose.transform.rotation;
                leftEye.transform.localRotation = pose.transform.localRotation;
                leftEye.transform.localScale = pose.transform.localScale;
                leftEye.transform.Translate(-leftEye.transform.right.normalized * (headPoseCamera.stereoSeparation * 0.5f));

                //right eye copy and adjust
                rightEye.transform.position = pose.transform.position;
                rightEye.transform.localPosition = pose.transform.localPosition;
                rightEye.transform.rotation = pose.transform.rotation;
                rightEye.transform.localRotation = pose.transform.localRotation;
                rightEye.transform.localScale = headPose.transform.localScale;
                rightEye.transform.Translate(rightEye.transform.right.normalized * (headPoseCamera.stereoSeparation * 0.5f));
            }
#endif
        }

        void OnEnable()
        {
            StartCoroutine(PresentStereoImagesCoroutine());

#if TILT_FIVE_SRP
            if(Application.isPlaying)
            {
                RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
                RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
            }
#endif
        }

#if TILT_FIVE_SRP
        private void OnDisable()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
        }
#endif

#if TILT_FIVE_SRP
        /// <summary>
        /// Configure rendering parameters for the upcoming frame.
        /// </summary>
        /// <remarks>This function primarily handles invalidated render textures due to fullscreen, alt+tabbing, etc.</remarks>
        private void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            // TODO: Determine whether this is necessary, or even permitted; the docs on RenderTexture.IsCreated() are lacking.
            // We want to check for invalidated render textures before rendering,
            // and this event should occur at the beginning of RenderPipeline.Render
            // before any actual render passes occur, so in principle this should work as a substitute for OnPreRender.

            // Check whether the left/right render textures' states have been invalidated,
            // and reset the cached texture handles if so. See the longer explanation below in Update()
            if (leftEyeCamera && rightEyeCamera && (!leftEyeCamera.targetTexture.IsCreated() || !rightEyeCamera.targetTexture.IsCreated()))
            {
                leftTexHandle = System.IntPtr.Zero;
                rightTexHandle = System.IntPtr.Zero;
            }
        }
#endif

        /// <summary>
        /// Configure rendering parameters for the upcoming frame.
        /// </summary>
        void OnPreRender()
        {
            theHeadPoseCamera.targetTexture = null;

            // Check whether the left/right render textures' states have been invalidated,
            // and reset the cached texture handles if so. See the longer explanation below in Update()
            if (!leftEyeCamera.targetTexture.IsCreated() || !rightEyeCamera.targetTexture.IsCreated())
            {
                leftTexHandle = System.IntPtr.Zero;
                rightTexHandle = System.IntPtr.Zero;
            }

            if(glassesMirrorMode == GlassesMirrorMode.None)
            {
                return;
            }

            // Lock the aspect ratio and add pillarboxing/letterboxing as needed.
            float screenRatio = Screen.width / (float)Screen.height;
            float targetRatio = glassesMirrorMode == GlassesMirrorMode.Stereoscopic
                ? DisplaySettings.stereoWidthToHeightRatio
                : DisplaySettings.monoWidthToHeightRatio;

            if(screenRatio > targetRatio) {
                // Screen or window is wider than the target: pillarbox.
                float normalizedWidth = targetRatio / screenRatio;
                float barThickness = (1f - normalizedWidth) / 2f;
                theHeadPoseCamera.rect = new Rect(barThickness, 0, normalizedWidth, 1);
            }
            else {
                // Screen or window is narrower than the target: letterbox.
                float normalizedHeight = screenRatio / targetRatio;
                float barThickness = (1f - normalizedHeight) / 2f;
                theHeadPoseCamera.rect = new Rect(0, barThickness, 1, normalizedHeight);
            }
        }


#if TILT_FIVE_SRP
        /// <summary>
        /// Apply post processing effects to the frame after it's finished rendering.
        /// </summary>
        /// <remarks>This function primarily updates the onscreen preview to reflect the glasses mirror mode.</remarks>
        /// <param name="context"></param>
        /// <param name="cameras"></param>
        void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (this == null || !enabled || glassesMirrorMode == GlassesMirrorMode.None)
            {
                return;
            }

            // OnEndFrameRendering isn't picky about the camera(s) that finished rendering.
            // This includes the scene view and/or material preview cameras.
            // We need to make sure we only run the code in this function when we're performing stereoscopic rendering.
            bool currentlyRenderingEyeCameras = false;

            for (int i = 0; i < cameras.Length; i++)
            {
#if UNITY_EDITOR
                if (cameras[i].Equals(UnityEditor.SceneView.lastActiveSceneView.camera))
                {
                    return;
                }
#endif
                if (cameras[i].Equals(leftEyeCamera) || cameras[i].Equals(rightEyeCamera) || cameras[i].Equals(headPoseCamera))
                {
                    currentlyRenderingEyeCameras = true;
                }
            }
            if (!currentlyRenderingEyeCameras)
            {
                return;
            }

            var leftTargetTex = leftEyeCamera.targetTexture;
            var rightTargetTex = rightEyeCamera.targetTexture;
            var previewTex = glassesMirrorMode == GlassesMirrorMode.Stereoscopic ? doublePreviewTex : singlePreviewTex;

            switch (glassesMirrorMode)
            {
                case GlassesMirrorMode.LeftEye:
                    CopyTextureToPreviewTextureSRP(commandBuffer, leftTargetTex, previewTex);
                    break;
                case GlassesMirrorMode.RightEye:
                    CopyTextureToPreviewTextureSRP(commandBuffer, rightTargetTex, previewTex);
                    break;
                case GlassesMirrorMode.Stereoscopic:
                    // Copy the two eyes' target textures to a double-wide texture, then display it onscreen.
                    CopyTextureToPreviewTextureSRP(commandBuffer, leftTargetTex, previewTex);
                    CopyTextureToPreviewTextureSRP(commandBuffer, rightTargetTex, previewTex, leftTargetTex.width);
                    break;
            }

            // Determine the aspect ratio to enable pillarboxing/letterboxing.
            float screenRatio = Screen.width / (float)Screen.height;
            float targetRatio = glassesMirrorMode == GlassesMirrorMode.Stereoscopic
                ? DisplaySettings.stereoWidthToHeightRatio
                : DisplaySettings.monoWidthToHeightRatio;
            Vector2 frameScale = Vector2.one;

            if (screenRatio != targetRatio)
            {
                frameScale = screenRatio > targetRatio
                    ? new Vector2(screenRatio / targetRatio, 1f)
                    : new Vector2(1f, targetRatio / screenRatio);
            }

            // We're going to composite the left/right eye cameras into a temporary render texture.
            var tempRTIdentifier = Shader.PropertyToID("TiltFiveCanvas");
            int tempRTWidth = (int)(previewTex.width * frameScale.x);
            int tempRTHeight = (int)(previewTex.height * frameScale.y);

            RenderTargetIdentifier currentRenderTarget = BuiltinRenderTextureType.CurrentActive;
            commandBuffer.GetTemporaryRT(tempRTIdentifier,
                tempRTWidth,
                tempRTHeight,
                previewTex.descriptor.depthBufferBits);
            commandBuffer.SetRenderTarget(tempRTIdentifier);
            commandBuffer.ClearRenderTarget(true, true, Color.black);
            commandBuffer.CopyTexture(previewTex, 0, 0, 0, 0,
                previewTex.width, previewTex.height,
                tempRTIdentifier, 0, 0,
                (tempRTWidth - previewTex.width) / 2, (tempRTHeight - previewTex.height) / 2);

            commandBuffer.Blit(tempRTIdentifier, headPoseCamera.targetTexture, Vector2.one, Vector2.zero);
            commandBuffer.ReleaseTemporaryRT(tempRTIdentifier);
            commandBuffer.SetRenderTarget(currentRenderTarget);
            context.ExecuteCommandBuffer(commandBuffer);
            context.Submit();
            commandBuffer.Clear();

            headPoseCamera.cullingMask = leftEyeCamera.cullingMask;
            headPoseCamera.clearFlags = leftEyeCamera.clearFlags;
        }
#endif

        void CopyTextureToPreviewTexture(RenderTexture sourceTex, RenderTexture destinationTex, int xOffset = 0)
        {
            Graphics.CopyTexture(
                        sourceTex,
                        0,      // srcElement
                        0,      // srcMip
                        0, 0,   // src offset
                        sourceTex.width, sourceTex.height,  // src size
                        destinationTex,
                        0,      // dstElement
                        0,      // dstMip
                        xOffset, 0);  // dst offset
        }

#if TILT_FIVE_SRP
        void CopyTextureToPreviewTextureSRP(CommandBuffer cmd, RenderTexture sourceTex, RenderTexture destinationTex, int xOffset = 0)
        {
            cmd.CopyTexture(
                sourceTex,
                0,
                0,
                0, 0,
                sourceTex.width, sourceTex.height,
                destinationTex,
                0, 0, xOffset, 0);
        }
#endif

        /// <summary>
        /// Apply post-processing effects to the final image before it is
        /// presented.
        /// </summary>
        /// <param name="src">The source render texture.</param>
        /// <param name="dst">The destination render texture.</param>
        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var leftTargetTex = leftEyeCamera.targetTexture;
            var rightTargetTex = rightEyeCamera.targetTexture;

            if(glassesMirrorMode != GlassesMirrorMode.None)
            {
                var previewTex = glassesMirrorMode == GlassesMirrorMode.Stereoscopic ? doublePreviewTex : singlePreviewTex;

                switch(glassesMirrorMode)
                {
                    case GlassesMirrorMode.LeftEye:
                        CopyTextureToPreviewTexture(leftTargetTex, previewTex);
                        break;
                    case GlassesMirrorMode.RightEye:
                        CopyTextureToPreviewTexture(rightTargetTex, previewTex);
                        break;
                    case GlassesMirrorMode.Stereoscopic:
                        // Copy the two eyes' target textures to a double-wide texture, then display it onscreen.
                        CopyTextureToPreviewTexture(leftTargetTex, previewTex);
                        CopyTextureToPreviewTexture(rightTargetTex, previewTex, leftTargetTex.width);
                        break;
                }

                // Blitting is required when overriding OnRenderImage().
                // Setting the blit destination to null is the same as blitting to the screen backbuffer.
                // This will effectively render previewTex to the screen.
                Graphics.Blit(previewTex,
                    null as RenderTexture,
                    Vector2.one,
                    Vector2.zero);
            }
            else Graphics.Blit(src, null as RenderTexture);

            // We're done with our letterboxing/pillarboxing now that we've blitted to the screen.
            // If the SplitStereoCamera gets disabled next frame, ensure that the original behavior returns.
            theHeadPoseCamera.rect = new Rect(0, 0, 1, 1);
        }

        IEnumerator PresentStereoImagesCoroutine()
        {
            // WaitForEndOfFrame() will let us wait until the last possible moment to send frames to the glasses.
            // This allows the results of rendering, postprocessing, and even GUI to be displayed.
            var cachedWaitForEndOfFrame = new WaitForEndOfFrame();

            while (enabled)
            {
                yield return cachedWaitForEndOfFrame;

                PresentStereoImages();
            }
        }

        private void PresentStereoImages()
        {
            var leftTargetTex = leftEyeCamera.targetTexture;
            var rightTargetTex = rightEyeCamera.targetTexture;
            bool isSrgb = leftTargetTex.sRGB;

            Vector3 posULVC_UWRLD = leftEyeCamera.transform.position;
            Quaternion rotToUWRLD_ULVC = leftEyeCamera.transform.rotation;
            Vector3 posURVC_UWRLD = rightEyeCamera.transform.position;
            Quaternion rotToUWRLD_URVC = rightEyeCamera.transform.rotation;

            Vector3 posULVC_UGBL = rotToUGBL_UWRLD * (scaleToUGBL_UWRLD * (posULVC_UWRLD - posUGBL_UWRLD));
            Quaternion rotToUGBL_ULVC = rotToUGBL_UWRLD * rotToUWRLD_ULVC;

            Vector3 posURVC_UGBL = rotToUGBL_UWRLD * (scaleToUGBL_UWRLD * (posURVC_UWRLD - posUGBL_UWRLD));
            Quaternion rotToUGBL_URVC = rotToUGBL_UWRLD * rotToUWRLD_URVC;


            /* Render textures have a state (created or not created), and that state can be invalidated.
            There are a few ways this can happen, including the game switching to/from fullscreen,
            or the system screensaver being displayed. When this happens, the native texture pointers we
            pass to the native plugin are also invalidated, and garbage data gets displayed by the glasses.

            To fix this, we can check whether the state has been invalidated and reacquire a valid native texture pointer.
            RenderTexture's IsCreated() function reports false if the render texture has been invalidated.
            We must detect this change above in OnPreRender(), because IsCreated reports true within Update().
            If we detect that the render textures have been invalidated, we null out the cached pointers and reacquire here.
            */
            if (leftTexHandle == System.IntPtr.Zero || rightTexHandle == System.IntPtr.Zero || Time.frameCount < 30)
            {
                leftTexHandle = leftTargetTex.GetNativeTexturePtr();
                rightTexHandle = rightTargetTex.GetNativeTexturePtr();
            }
            Display.PresentStereoImages(leftTexHandle, rightTexHandle,
                                       leftTargetTex.width, leftTargetTex.height,
                                       isSrgb,
                                       fieldOfView,
                                       DisplaySettings.monoWidthToHeightRatio,
                                       rotToUGBL_ULVC,
                                       posULVC_UGBL,
                                       rotToUGBL_URVC,
                                       posURVC_UGBL);
        }

        /// <summary>
        /// Syncs the Cameras' fields to the input parameter.
        /// </summary>
        /// <param name="theCamera">The camera to read from.</param>
        void SyncFields(Camera theCamera)
        {
            fieldOfView = theCamera.fieldOfView;
            nearClipPlane = theCamera.nearClipPlane;
            farClipPlane = theCamera.farClipPlane;
            aspectRatio = theCamera.aspect;
        }

        /// <summary>
        /// EDITOR-ONLY
        /// </summary>
        void OnValidate()
        {

#if UNITY_EDITOR
            if (false == UnityEditor.EditorApplication.isPlaying)
                return;
#endif
            if (null == headPoseCamera)
                return;

            if (null != leftEye && null != rightEye)
                showHideCameras();

            SyncFields(headPoseCamera);
            SyncTransform();
        }

        /// <summary>
        /// Show/hide to the eye camerasin the hierarchy.
        /// </summary>
        void showHideCameras()
        {
            if (showCameras)
            {
                leftEye.hideFlags = HideFlags.None;
                rightEye.hideFlags = HideFlags.None;
            }
            else
            {
                leftEye.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                rightEye.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            }
        }
    }
}
