using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DirectShowLib;

namespace WPFMediaKit.DirectShow.Controls
{
    public class MultimediaUtil
    {
        #region Audio Renderer Methods
        /// <summary>
        /// The private cache of the audio renderer names
        /// </summary>
        private static string[] m_audioRendererNames;

        /// <summary>
        /// An array of audio renderer device names
        /// on the current system
        /// </summary>
        public static string[] AudioRendererNames
        {
            get
            {
                if (m_audioRendererNames == null)
                {
                    m_audioRendererNames = (from a in GetDevices(FilterCategory.AudioRendererCategory)
                                            select a.Name).ToArray();
                }
                return m_audioRendererNames;
            }
        }
        #endregion

        #region Video Input Devices
        /// <summary>
        /// The private cache of the video input names
        /// </summary>
        private static string[] m_videoInputNames;

        /// <summary>
        /// An array of video input device names
        /// on the current system
        /// </summary>
        public static string[] VideoInputNames
        {
            get
            {
                if (m_videoInputNames == null)
                {
                    m_videoInputNames = (from d in VideoInputDevices
                                         select d.Name).ToArray();
                }
                return m_videoInputNames;
            }
        }

        #endregion

        private static DsDevice[] GetDevices(Guid filterCategory)
        {
            return (from d in DsDevice.GetDevicesOfCat(filterCategory)
                    select d).ToArray();
        }

        public static DsDevice[] VideoInputDevices
        {
            get
            {
                if (m_videoInputDevices == null)
                {
                    m_videoInputDevices = GetDevices(FilterCategory.VideoInputDevice);
                }
                return m_videoInputDevices;
            }
        }
        private static DsDevice[] m_videoInputDevices;

        public static string[] VideoInputsDevicePaths
        {
            get
            {
                if (m_videoInputsDevicePaths == null)
                {
                    m_videoInputsDevicePaths = (from d in VideoInputDevices
                                                select d.DevicePath).ToArray();
                }
                return m_videoInputsDevicePaths;
            }
        }
        private static string[] m_videoInputsDevicePaths;


        public static IReadOnlyList<CameraParmeter> GetCameraParmeters(string cameraName, IAMStreamConfig videoStreamConfig = null)
        {
            var d = VideoInputDevices.FirstOrDefault(d => d.Name == cameraName);
            return GetCameraParmeters(d, videoStreamConfig);
        }

        /// <summary>
        /// Key:DevicePath
        /// </summary>
        private static System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyList<CameraParmeter>> _cameraParmeters = new();
        public static IReadOnlyList<CameraParmeter> GetCameraParmeters(DsDevice device, IAMStreamConfig videoStreamConfig = null)
        {
            if (_cameraParmeters.TryGetValue(device.DevicePath, out var parmeters))
            {
                return parmeters;
            }

            int hr = 0;

            if (videoStreamConfig == null)
            {
                IGraphBuilder m_graph = null;
                ICaptureGraphBuilder2 graphBuilder = null;
                IBaseFilter captureFilter = null;
                try
                {
                    m_graph = (IGraphBuilder)new FilterGraphNoThread();

                    graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

                    hr = graphBuilder.SetFiltergraph(m_graph);
                    DsError.ThrowExceptionForHR(hr);

                    if (m_graph is not IFilterGraph2 filterGraph)
                        return null;

                    hr = filterGraph.AddSourceFilterForMoniker(device.Mon, null, device.Name, out captureFilter);
                    DsError.ThrowExceptionForHR(hr);

                    hr = graphBuilder.FindInterface(PinCategory.Capture,
                                                    MediaType.Video,
                                                    captureFilter,
                                                    typeof(IAMStreamConfig).GUID,
                                                    out var streamConfig);
                    DsError.ThrowExceptionForHR(hr);

                    videoStreamConfig = streamConfig as IAMStreamConfig;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    if (captureFilter != null)
                    {
                        Marshal.FinalReleaseComObject(captureFilter);
                        captureFilter = null;
                    }
                    if (graphBuilder != null)
                    {
                        Marshal.FinalReleaseComObject(graphBuilder);
                        graphBuilder = null;
                    }
                    if (m_graph != null)
                    {
                        Marshal.FinalReleaseComObject(m_graph);
                        m_graph = null;
                    }
                }
            }

            if (videoStreamConfig == null)
            {
                throw new WPFMediaKitException("Failed to get IAMStreamConfig");
            }

            var temp = new List<CameraParmeter>();
            hr = videoStreamConfig.GetNumberOfCapabilities(out var count, out _);
            var vscc = new VideoStreamConfigCaps();
            var vscc_ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(vscc));
            for (int i = 0; i < count; i++)
            {
                hr = videoStreamConfig.GetStreamCaps(i, out var t, vscc_ptr);
                if (hr == 0 && t.majorType == MediaType.Video && t.formatType == FormatType.VideoInfo)
                {
                    var info = new VideoInfoHeader();
                    Marshal.PtrToStructure(t.formatPtr, info);
                    temp.Add(new CameraParmeter()
                    {
                        Width = info.BmiHeader.Width,
                        Height = info.BmiHeader.Height,
                        FPS = (int)(MediaPlayers.MediaPlayerBase.DSHOW_ONE_SECOND_UNIT / info.AvgTimePerFrame),
                        SubType = t.subType,
                        SubTypeName = MediaSubTypes.TryGetValue(t.subType, out var f) ? f : "Unknow",
                    });
                }
                DsUtils.FreeAMMediaType(t);
            }
            Marshal.FreeCoTaskMem(vscc_ptr);
            temp = temp.OrderByDescending(d => d.Size).ThenByDescending(d => d.FPS).ToList();
            _cameraParmeters.AddOrUpdate(device.DevicePath, temp, (k, v) => temp);
            return temp;

        }

        private static Lazy<Dictionary<Guid, string>> _mediaSubTypes = new(() =>
        {
            var type = typeof(MediaSubType);
            var temp = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var dict = new Dictionary<Guid, string>();
            foreach (var t in temp)
            {
                dict[(Guid)t.GetValue(null)] = t.Name;
            }
            return dict;
        });
        public static IReadOnlyDictionary<Guid, string> MediaSubTypes => _mediaSubTypes.Value;
    }
}
