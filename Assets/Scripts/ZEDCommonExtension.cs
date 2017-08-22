using System.Runtime.InteropServices;

namespace sl.extension
{
    public enum ERROR_CODE
    {
        /// <summary>
        ///  Every step went fine, the camera is ready to use
        /// </summary>
        SUCCESS,
        /// <summary>
        /// unsuccessful behavior
        /// </summary>
        FAILURE,
        /// <summary>
        /// No GPU found or CUDA capability of the device is not supported
        /// </summary>
        NO_GPU_COMPATIBLE,
        /// <summary>
        /// Not enough GPU memory for this depth mode, please try a faster mode (such as PERFORMANCE mode)
        /// </summary>
        NOT_ENOUGH_GPUMEM,
        /// <summary>
        /// The ZED camera is not plugged or detected 
        /// </summary>
        CAMERA_NOT_DETECTED,
        /// <summary>
        /// For Jetson only, resolution not yet supported (USB3.0 bandwidth)
        /// </summary>
        INVALID_RESOLUTION,
        /// <summary>
        /// This issue can occurs when you use multiple ZED or a USB 2.0 port (bandwidth issue)
        /// </summary>
        LOW_USB_BANDWIDTH,
        /// <summary>
        /// ZED Settings file is not found on the host machine. Use ZED Settings tool to correct it.
        /// </summary>
        CALIBRATION_FILE_NOT_AVAILABLE,
        /// <summary>
        /// The provided SVO file is not valid
        /// </summary>
        INVALID_SVO_FILE,
        /// <summary>
        /// An recorder related error occurred (not enough free storage, invalid file)
        /// </summary>
        SVO_RECORDING_ERROR,
        /// <summary>
        ///  The requested coordinate system is not available.
        /// </summary>
        INVALID_COORDINATE_SYSTEM,
        /// <summary>
        /// The firmware of the ZED is out of date. You might install the new version.
        /// </summary>
        INVALID_FIRMWARE,
        /// <summary>
        /// An invalid parameter has been set for the function. ADDED
        /// </summary>
        INVALID_FUNCTION_PARAMETERS,
        /// <summary>
        /// in grab() only, the current call return the same frame as last call. Not a new frame.
        /// </summary>
        NOT_A_NEW_FRAME,
        /// <summary>
        /// in grab() only, a CUDA error has been detected in the process.
        /// </summary>
        CUDA_ERROR,
        /// <summary>
        /// in grab() only, ZED SDK is not initialized. Probably a missing call to sl.Camera.Init.
        /// </summary>
        CAMERA_NOT_INITIALIZED,
        /// <summary>
        /// your NVIDIA driver is too old and not compatible with your current CUDA version.
        /// </summary>
        NVIDIA_DRIVER_OUT_OF_DATE,
        /// <summary>
        /// the call of the function is not valid in the current context. Could be a missing call of sl.Camera.Init
        /// </summary>
        INVALID_FUNCTION_CALL,
        /// <summary>
        /// The SDK wasn't able to load its dependencies, the installer should be launched.
        /// </summary>
        CORRUPTED_SDK_INSTALLATION,
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct UInt3
    {
        public int x;
        public int y;
        public int z;

        public UInt3(int _x, int _y, int _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
    }
}
