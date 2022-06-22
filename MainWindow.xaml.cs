using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace biometria_proj_2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static Thread _beepThread;
        static AutoResetEvent _signalBeep;
        bool startable = false;
        VideoCaptureDevice device;
        static readonly CascadeClassifier eyeClassifier = new CascadeClassifier(@"./haarcascade_eye.xml");
        static readonly CascadeClassifier faceClassifier = new CascadeClassifier(@"./haarcascade_frontalface_alt.xml");
        FacemarkLBFParams facemarkLBFParams = new();
        FacemarkLBF facemark;
        const double EYE_AR_THRESH = 0.3;
        const int EYE_AR_CONSEC_FRAMES = 3;
        // initialize the frame counters and the total number of blinks
        int counter = 0;
        int total = 0;
        string devicename;
        public MainWindow()
        {
            InitializeComponent();
            filter = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in filter)
            {
                comboBox.Items.Add(device.Name);
            }
            comboBox.SelectedIndex = 0;
        }
        FilterInfoCollection filter;
        private void initVideo(object sender, RoutedEventArgs e)
        {
            facemark = new(facemarkLBFParams);
            //pass absolute path
            facemark.LoadModel(@"lbfmodel.yaml");

            if (comboBox.SelectedItem != null)
            {
                devicename = filter[comboBox.SelectedIndex].MonikerString;
            }
            if (String.IsNullOrEmpty(devicename))
            {
                return;
            }
            device = new VideoCaptureDevice(devicename);
            device.NewFrame += new NewFrameEventHandler(Device_NewFrame);
            if (device.IsRunning)
                device.Stop();
            device.Start();
        }

        private void Device_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone();
            //do stuff


            Image<Bgr, byte> grayScale = bitmap.ToImage<Bgr, byte>();
            //scaling options
            System.Drawing.Rectangle[] rectangles = faceClassifier.DetectMultiScale(grayScale, 1.1, 3);
            foreach (System.Drawing.Rectangle rectangle in rectangles)
            {
                System.Drawing.Rectangle[] eyes = eyeClassifier.DetectMultiScale(grayScale, 1.1, 3);
                foreach (System.Drawing.Rectangle eye in eyes)

                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        using (Pen pen = new Pen(Color.GreenYellow, 1))
                        {
                            g.DrawRectangle(pen, eye);

                        }
                    }

            }
            VectorOfVectorOfPointF landmarks = new();
            VectorOfRect rects = new(rectangles);
            bool sleepyBoy = false;
            if (facemark.Fit(grayScale, rects, landmarks))
            {
                for (int i = 0; i < rectangles.Length; i++)
                {
                    var  x = landmarks.ToArrayOfArray();
                    if (x[i].Length < 47)
                        return;
                    var leftEyePoints = x[i].Skip(36).Take(6);
                    var rightEyePoints = x[i].Skip(42).Take(6);
                    sleepyBoy = Eye_Aspect_Ratio(leftEyePoints.ToArray()) <0.22 && Eye_Aspect_Ratio(rightEyePoints.ToArray()) < 0.22;
                }
            }
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                VideoStream.Source = Convert(bitmap);
            }));

            if (sleepyBoy)
                counter++;

            if (counter > 10)
            {
                counter = 0;
                System.Media.SystemSounds.Beep.Play();
            }

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (device != null && device.IsRunning)
            {
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, (SendOrPostCallback)delegate
                {
                    device.SignalToStop();
                    device.WaitForStop();
                    device = null;
                }, null);
            }
        }

        public BitmapImage Convert(Bitmap src)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                src.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                bitmapimage.Freeze();
                return bitmapimage;
            }
        }

        public double Eye_Aspect_Ratio(PointF[] eye)
        {
            double distance(PointF A, PointF B)
            {
                return Math.Sqrt(Math.Pow(A.X - B.X, 2) + Math.Pow(A.Y - B.Y, 2));
            }
            var A = distance(eye[1], eye[5]);
            var B = distance(eye[2], eye[4]);
            var C = distance(eye[0], eye[3]);
            var ear = (A + B) / (2.0 * C);
            return ear;
        }
    }
}
