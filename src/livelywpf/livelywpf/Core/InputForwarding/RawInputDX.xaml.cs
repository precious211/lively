﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Linearstar.Windows.RawInput;

namespace livelywpf.Core
{
    /// <summary>
    /// Mouseinput retrival and forwarding to wallpaper using DirectX RawInput.
    /// ref: https://docs.microsoft.com/en-us/windows/win32/inputdev/raw-input
    /// </summary>
    public partial class RawInputDX : Window
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public InputForwardMode InputMode { get; private set; }
        public RawInputDX(InputForwardMode inputMode)
        {
            InitializeComponent();
            //Starting a hidden window outside screen region.
            //todo: Other wrappers such as SharpDX:https://github.com/sharpdx/SharpDX does not require a window, could not get it to work properly globally.. investigate.
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = -99999;
            SourceInitialized += Window_SourceInitialized;
            this.InputMode = inputMode;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var windowInteropHelper = new WindowInteropHelper(this);
            var hwnd = windowInteropHelper.Handle;

            switch (InputMode)
            {
                case InputForwardMode.off:
                    break;
                case InputForwardMode.mouse:
                    //ExInputSink flag makes it work even when not in foreground, similar to global hook.. but asynchronous, no complications and no AV false detection!
                    RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
                        RawInputDeviceFlags.ExInputSink, hwnd);
                    break;
                case InputForwardMode.mousekeyboard:
                    RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
                        RawInputDeviceFlags.ExInputSink, hwnd);
                    RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard,
                        RawInputDeviceFlags.ExInputSink, hwnd);
                    break;
            }

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(Hook);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            switch (InputMode)
            {
                case InputForwardMode.off:
                    break;
                case InputForwardMode.mouse:
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
                    break;
                case InputForwardMode.mousekeyboard:
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);
                    break;
            }
        }

        protected IntPtr Hook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            // You can read inputs by processing the WM_INPUT message.
            if (msg == (int)NativeMethods.WM.INPUT)
            {
                // Create an RawInputData from the handle stored in lParam.
                var data = RawInputData.FromHandle(lparam);

                // You can identify the source device using Header.DeviceHandle or just Device.
                //var sourceDeviceHandle = data.Header.DeviceHandle;
                //var sourceDevice = data.Device;

                // The data will be an instance of either RawInputMouseData, RawInputKeyboardData, or RawInputHidData.
                // They contain the raw input data in their properties.
                switch (data)
                {
                    case RawInputMouseData mouse:
                        //RawInput only gives relative mouse movement value.. cheating here with Winform library.
                        var M = System.Windows.Forms.Control.MousePosition;
                        switch (mouse.Mouse.Buttons)
                        {
                            case Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.LeftButtonDown:
                                ForwardMessageMouse(M.X, M.Y, (int)NativeMethods.WM.LBUTTONDOWN, (IntPtr)0x0001);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.LeftButtonUp:
                                ForwardMessageMouse(M.X, M.Y, (int)NativeMethods.WM.LBUTTONUP, (IntPtr)0x0001);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.RightButtonDown:
                                //issue: click being skipped; desktop already has its own rightclick contextmenu.
                                //ForwardMessage(M.X, M.Y, (int)NativeMethods.WM.RBUTTONDOWN, (IntPtr)0x0002);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.RightButtonUp:
                                //issue: click being skipped; desktop already has its own rightclick contextmenu.
                                //ForwardMessage(M.X, M.Y, (int)NativeMethods.WM.RBUTTONUP, (IntPtr)0x0002);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.None:
                                ForwardMessageMouse(M.X, M.Y, (int)NativeMethods.WM.MOUSEMOVE, (IntPtr)0x0020);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.MouseWheel:
                                //Disabled, not tested yet.
                                /*
                                https://github.com/ivarboms/game-engine/blob/master/Input/RawInput.cpp
                                Mouse wheel deltas are represented as multiples of 120.
                                MSDN: The delta was set to 120 to allow Microsoft or other vendors to build
                                finer-resolution wheels (a freely-rotating wheel with no notches) to send more
                                messages per rotation, but with a smaller value in each message.
                                Because of this, the value is converted to a float in case a mouse's wheel
                                reports a value other than 120, in which case dividing by 120 would produce
                                a very incorrect value.
                                More info: http://social.msdn.microsoft.com/forums/en-US/gametechnologiesgeneral/thread/1deb5f7e-95ee-40ac-84db-58d636f601c7/
                                */

                                /*
                                // One wheel notch is represented as this delta (WHEEL_DELTA).
                                const float oneNotch = 120;

                                // Mouse wheel delta in multiples of WHEEL_DELTA (120).
                                float mouseWheelDelta = mouse.Mouse.RawButtons;

                                // Convert each notch from [-120, 120] to [-1, 1].
                                mouseWheelDelta = mouseWheelDelta / oneNotch;

                                MouseScrollSimulate(mouseWheelDelta);
                                */
                                break;
                        }
                        break;
                    case RawInputKeyboardData keyboard:
                        switch (keyboard.Keyboard.Flags)
                        {
                            case Linearstar.Windows.RawInput.Native.RawKeyboardFlags.Down:
                                ForwardMessageKeyboard((int)NativeMethods.WM.KEYDOWN, (IntPtr)keyboard.Keyboard.VirutalKey);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawKeyboardFlags.Up:
                                //ForwardMessageKeyboard((int)NativeMethods.WM.KEYUP, (IntPtr)keyboard.Keyboard.VirutalKey);
                                break;
                            case Linearstar.Windows.RawInput.Native.RawKeyboardFlags.LeftKey:
                                break;
                            case Linearstar.Windows.RawInput.Native.RawKeyboardFlags.RightKey:
                                break;
                        }
                        System.Diagnostics.Debug.WriteLine(keyboard.Keyboard);
                        break;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Forwards the keyboard message to the required wallpaper window based on given cursor location.<br/>
        /// Skips if desktop is not focused.
        /// </summary>
        /// <param name="msg">key press msg</param>
        /// <param name="wParam">Virtual-Key code</param>
        private static void ForwardMessageKeyboard(int msg, IntPtr wParam)
        {
            try
            {
                if (Playback.IsDesktop())
                {
                    //Detect active wp based on cursor pos.
                    //Better way to do this?
                    var display = Screen.FromPoint(new System.Drawing.Point(
                        System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y));
                    SetupDesktop.Wallpapers.ForEach(wallpaper =>
                    {
                        if (IsInputAllowed(wallpaper.GetWallpaperType()))
                        {
                            if (ScreenHelper.ScreenCompare(display, wallpaper.GetScreen(), DisplayIdentificationMode.screenLayout) ||
                                    Program.SettingsVM.Settings.WallpaperArrangement == WallpaperArrangement.span)
                            {
                                //TODO: provide lParam (check docs of wm-keydown and up), weirdly enough most keys works without it.
                                //Problems: 
                                //Some keys don't work - arrow keys..
                                //Repeated key input when keyUp msg is sent.
                                //Ref:
                                //https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-keydown
                                //https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-keyup
                                NativeMethods.PostMessageW(wallpaper.GetHWND(), msg, wParam, IntPtr.Zero);
                            }
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Logger.Error("Input Forwarding Error:" + e.Message);
            }
        }

        /// <summary>
        /// Forwards the mouse message to the required wallpaper window based on given cursor location.<br/>
        /// Skips if apps are in foreground.
        /// </summary>
        /// <param name="x">Cursor pos x</param>
        /// <param name="y">Cursor pos y</param>
        /// <param name="msg">mouse message</param>
        /// <param name="wParam">additional msg parameter</param>
        private static void ForwardMessageMouse(int x, int y, int msg, IntPtr wParam)
        {
            //Don't forward when not on desktop.
            if (!Playback.IsDesktop())
            {
                if (msg != (int)NativeMethods.WM.MOUSEMOVE || !Program.SettingsVM.Settings.MouseInputMovAlways)
                {
                    return;
                }
            }

            try
            {
                var display = Screen.FromPoint(new System.Drawing.Point(x, y));
                var mouse = CalculateMousePos(x, y, display);
                SetupDesktop.Wallpapers.ForEach(wallpaper =>
                {
                    if (IsInputAllowed(wallpaper.GetWallpaperType()))
                    {
                        if (ScreenHelper.ScreenCompare(display, wallpaper.GetScreen(), DisplayIdentificationMode.screenLayout) ||
                            Program.SettingsVM.Settings.WallpaperArrangement == WallpaperArrangement.span)
                        {
                            //The low-order word specifies the x-coordinate of the cursor, the high-order word specifies the y-coordinate of the cursor.
                            //ref: https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-mousemove
                            uint lParam = Convert.ToUInt32(mouse.Y);
                            lParam <<= 16;
                            lParam |= Convert.ToUInt32(mouse.X);
                            NativeMethods.PostMessageW(wallpaper.GetHWND(), msg, wParam, (IntPtr)lParam);
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Error("Input Forwarding Error:" + e.Message);
            }
        }

        /// <summary>
        /// Converts global mouse cursor position value to per display localised value.
        /// </summary>
        /// <param name="x">Cursor pos x</param>
        /// <param name="y">Cursor pos y</param>
        /// <param name="display">Target display device</param>
        /// <returns>Localised cursor value</returns>
        private static Point CalculateMousePos(int x, int y, Screen display)
        {
            if (ScreenHelper.IsMultiScreen())
            {
                if (Program.SettingsVM.Settings.WallpaperArrangement == WallpaperArrangement.span)
                {
                    x -= SystemInformation.VirtualScreen.Location.X;
                    y -= SystemInformation.VirtualScreen.Location.Y;
                }
                else //per-display or duplicate mode.
                {
                    x += -1 * display.Bounds.X;
                    y += -1 * display.Bounds.Y;
                }
            }
            return new Point(x, y);
        }

        private static bool IsInputAllowed(WallpaperType type)
        {
            bool result = false;
            switch (type)
            {
                case WallpaperType.app:
                    result = true;
                    break;
                case WallpaperType.web:
                    result = true;
                    break;
                case WallpaperType.webaudio:
                    result = true;
                    break;
                case WallpaperType.url:
                    result = true;
                    break;
                case WallpaperType.bizhawk:
                    result = true;
                    break;
                case WallpaperType.unity:
                    result = true;
                    break;
                case WallpaperType.godot:
                    result = true;
                    break;
                case WallpaperType.video:
                    result = false;
                    break;
                case WallpaperType.gif:
                    result = false;
                    break;
                case WallpaperType.unityaudio:
                    result = true;
                    break;
                case WallpaperType.videostream:
                    result = false;
                    break;
                case WallpaperType.picture:
                    result = false;
                    break;
            }
            return result;
        }
    }
}