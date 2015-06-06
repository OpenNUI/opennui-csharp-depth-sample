using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows.Threading;
using System.IO;
using OpenNUI.CSharp.Library;

namespace OpenNUI.Samples.DepthBasics
{
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Instant of NuiApplication for Connect OpenNUI Service
        /// </summary>
        NuiApplication nuiApp = null;

        /// <summary>
        /// Instant of NuiSensor
        /// </summary>
        NuiSensor useSensor = null;
        /// <summary>
        /// the Timer for getting frames from OpenNUI Service
        /// </summary>
        private System.Timers.Timer openNUIFrameTimer;
        /// <summary>
        /// worker for openNUIFrameTimer
        /// </summary>
        Action act;

        /// <summary>
        /// bitmap for drawing depthframe
        /// </summary>
        WriteableBitmap bitmap = null;

        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // create nuiApp instance and setting NuiApplication's name
            nuiApp = new NuiApplication("OpenNUI.Samples.DepthBasics");

            // register delegate when nui sensor connected, disconnected
            nuiApp.OnSensorConnected += nuiApp_OnSensorConnected;
            nuiApp.OnSensorDisconnected += nuiApp_OnSensorDisconnected;

            try
            {
                nuiApp.Start();
            }
            // when exception catched. it means OpenNUI dosen't installed
            catch
            {
                //show error messagebox
                MessageBox.Show("OpenNUI dosen't installed", "error", MessageBoxButton.OK, MessageBoxImage.Error);

                //program exit
                Environment.Exit(0);
            }

            // worker for openNUIFrameTimer
            act = new Action(delegate()
            {
                //do not work when usesensor is null
                if (useSensor == null)
                    return;

                //get the depth frame
                DepthData depthFrame = useSensor.GetDepthFrame();

                //do not work when depth frame is null (faild to GetDepthFrame())
                if (depthFrame == null)
                    return;

                for (int i = 0; i < depthFrame.FrameData.Length; ++i)
                {
                    // Get the depth for this pixel
                    ushort depth = depthFrame.FrameData[i];

                    // To convert to a byte, we're mapping the depth value to the byte range.
                    // Values outside the reliable depth range are mapped to 0 (black).
                    ushort minDepth = 0;
                    ushort maxDepth = ushort.MaxValue;
                    this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
                }

                bitmap.WritePixels(
                    new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
                    this.depthPixels,
                    bitmap.PixelWidth,
                    0);

                depthImage.Source = bitmap;

            });


            // create openNUIFrameTimer instance
            openNUIFrameTimer = new System.Timers.Timer();

            //set the timer interval to 60fps
            openNUIFrameTimer.Interval = 1000 / 60;

            //set the timer elapsed callback
            openNUIFrameTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
            {
                try
                {
                    //use invoke for access to the this wpf window's ui
                    Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, act);
                }
                catch { }
            };

            //set the timer autoreset to true for loop
            openNUIFrameTimer.AutoReset = true;

            //openNUIFrameTimer start
            openNUIFrameTimer.Start();

            // initialize the components (controls) of the window
            InitializeComponent();
        }

        /// <summary>
        /// Handles the nui sensor disconnected
        /// </summary>
        /// <param name="sensor">connected sensor</param>
        void nuiApp_OnSensorDisconnected(NuiSensor sensor)
        {
            if (useSensor == sensor)
            {
                //set usesensor to null when usesensor is disconnected
                useSensor = null;

                //set bitmap to null
                bitmap = null;

                //set depthPixels to null
                depthPixels = null;

                //use invoke for access to the this wpf window's ui
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    //init sensor's name,vendor textbox
                    sensorNameTB.Text = "";
                    sensorVendorTB.Text = "";
                }));
            }
        }

        /// <summary>
        /// Handles the nui sensor connected
        /// </summary>
        /// <param name="sensor">connected sensor</param>
        void nuiApp_OnSensorConnected(NuiSensor sensor)
        {
            if (useSensor == null)
            {
                //set usesensor to now connected sensor when usesensor is empty
                useSensor = sensor;

                // allocate space to put the pixels being received and converted
                depthPixels = new byte[useSensor.DepthInfo.Width * useSensor.DepthInfo.Height];

                //open depth frame
                useSensor.OpenDepthFrame();

                //use invoke for access to the this wpf window's ui
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    //create bitmap instance
                    bitmap = new WriteableBitmap(useSensor.DepthInfo.Width, useSensor.DepthInfo.Height, 96.0, 96.0, PixelFormats.Gray8, null);

                    //set the depthImage's source to depthframe's bitmap
                    depthImage.Source = bitmap;

                    //update sensor's name,vendor textbox
                    sensorNameTB.Text = useSensor.Name;
                    sensorVendorTB.Text = useSensor.Vendor;
                }));
            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Screenshot_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.bitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(this.bitmap));

                string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat);

                string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string path = System.IO.Path.Combine(myPhotos, "OpenNUIScreenshot-Depth-" + time + ".png");

                // write the new file to disk
                try
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                }
                catch (IOException)
                {

                }
            }
        }

    }
}
