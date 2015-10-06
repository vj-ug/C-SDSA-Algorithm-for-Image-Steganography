using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Steganography
{
    public partial class Steganography : Form
    {
        OpenFileDialog ofd = new OpenFileDialog();
        SaveFileDialog sfd = new SaveFileDialog();

        const string Filter = "Image Files (*.png) | *.png";
        const string StartDir = @"./Images";

        string _binImg = "";

        public Steganography()
        {
            InitializeComponent();

            ofd.Filter = Filter;
            ofd.InitialDirectory = StartDir;

            sfd.Filter = Filter;
            sfd.InitialDirectory = StartDir;
        }

        #region #### Open Images ####
        private void btnOpenBaseImage_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                pbBaseImage.Image = new Bitmap(ofd.FileName);
            }
        }

        private void btnOpenHideImage_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                pbHideImage.Image = new Bitmap(ofd.FileName);

                _binImg = ByteArrayToBinary(File.ReadAllBytes(ofd.FileName));
            }
        }
        #endregion

        #region #### Hide Image ####
        /// <summary>
        /// Hide an image inside of another image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnHideImage_Click(object sender, EventArgs e)
        {
            // We only want to run the code below if we have images to work with.
            if (pbBaseImage == null)
            {
                return;
            }
            
            if (pbHideImage.Image == null)
            {
                return;
            }

            // Counter used to keep track of what bit we are at
            int dataWriteCtr = 0;
            // The data to be written
            char[] data = _binImg.ToCharArray();

            // Length of the data
            int msgLenWriteCtr = 0;

            // Convert length of the data into binary
            char[] dataLen = Convert.ToString(data.Length, 2).PadLeft(24, '0').ToCharArray();
            
            // We only write the length of the data in the last 6 pixels, max message is 16777215 bits.
            if (data.Length > 16777215) // Last 6 pixels
                return;

            // Image to have data written too.
            var img = (Bitmap)pbBaseImage.Image;

            // If the data is too big to be stored inside the image, return.
            // We -6 because the last 6 pixels store the data length
            if ((data.Length / 4) > (img.Width * img.Height) - 6)
                return;

            // Used in changing pixels in the image
            var newPixel = new Pixel();

            // Loop over every pixel but the last 2
            for (int x = 0; x < img.Width; x++)
            {
                for (int y = 0; y < img.Height; y++)
                {
                    var currPixel = img.GetPixel(x, y);
                    newPixel.A = currPixel.A;
                    newPixel.R = currPixel.R;
                    newPixel.G = currPixel.G;
                    newPixel.B = currPixel.B;

                    if (ProcessingImageLastSixPixels(img, x, y))
                    {
                        newPixel.A = SetPixelChannel(currPixel.A, dataLen, ref msgLenWriteCtr);
                        newPixel.R = SetPixelChannel(currPixel.R, dataLen, ref msgLenWriteCtr);
                        newPixel.G = SetPixelChannel(currPixel.G, dataLen, ref msgLenWriteCtr);
                        newPixel.B = SetPixelChannel(currPixel.B, dataLen, ref msgLenWriteCtr);
                    }
                    else if (dataWriteCtr < data.Length)
                    {
                        newPixel.A = SetPixelChannel(currPixel.A, data, ref dataWriteCtr);
                        newPixel.R = SetPixelChannel(currPixel.R, data, ref dataWriteCtr);
                        newPixel.G = SetPixelChannel(currPixel.G, data, ref dataWriteCtr);
                        newPixel.B = SetPixelChannel(currPixel.B, data, ref dataWriteCtr);
                    }
                    /*else // Uncomment if you want to show what pixels are being modified
                    {
                        newPixel.A = 255;
                        newPixel.R = 255;
                        newPixel.G = 255;
                        newPixel.B = 255;
                    }*/

                    img.SetPixel(x, y, Color.FromArgb(newPixel.A, newPixel.R, newPixel.G, newPixel.B));
                }
            }

            // Save the file
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                img.Save(sfd.FileName);
            }
        }

        /// <summary>
        /// Are the X and Y cood's the last 6 pixels in any given image?
        /// </summary>
        /// <param name="img"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static bool ProcessingImageLastSixPixels(Bitmap img, int x, int y)
        {
            // Images are 0-x indexed so to get to the last 6 pixels we take 
            // 6 + 1 from the width and 1 + 1 from the height to get to those pixels.
            return x > (img.Width - 7) && y > (img.Height - 2);
        }

        /// <summary>
        /// Store one bit in a channel of a pixel
        /// There is 4 channels, Alpha, Red, Green, Blue.
        /// </summary>
        /// <param name="currPixelChannel"></param>
        /// <param name="data"></param>
        /// <param name="msgWriteCtr"></param>
        /// <returns></returns>
        private static int SetPixelChannel(byte currPixelChannel, char[] data, ref int msgWriteCtr)
        {
            int newPixelChannel;

            // If the current pixel's channel value is odd
            // then we want to check the msg for the current bit
            if (currPixelChannel % 2 == 1)
            {
                // If the bit we want to write is 1
                if (data[msgWriteCtr++] == '1')
                {
                    // save the Alpha value for later
                    newPixelChannel = currPixelChannel;
                }
                else // its 0
                {
                    // change the Alpha value by 1 and save for later
                    newPixelChannel = currPixelChannel - 1;
                }
            }
            else // its even
            {
                // if the bit we want to write is 1
                if (data[msgWriteCtr++] == '1')
                {
                    // change the Alpha value by 1 and save for later
                    newPixelChannel = currPixelChannel + 1;
                }
                else // its 0
                {
                    // save the Alpha value for later
                    newPixelChannel = currPixelChannel;
                }
            }

            return newPixelChannel;
        }
        #endregion

        #region #### Recover Image ####
        /// <summary>
        /// Recover data hidden inside an image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRecoverImage_Click(object sender, EventArgs e)
        {
            // Only run the code below if we have an image to work with.
            if (pbBaseImage.Image == null)
                return;

            var img = (Bitmap)pbBaseImage.Image;
            var bitStream = "";

            // Get the length of the data
            for (int x = img.Width - 6; x < img.Width; x++)
            {
                int y = img.Height - 1;
                var currPixel = img.GetPixel(x, y);

                bitStream = GetNybbleFromPixelChannels(currPixel, bitStream);
            }

            int dataLen = Convert.ToInt32(bitStream, 2);
            int dataCtr = 0;
            bitStream = "";

            // Get the data stored inside the image
            for (int x = 0; x < img.Width; x++)
            {
                for (int y = 0; y < img.Height; y++)
                {
                    var currPixel = img.GetPixel(x, y);

                    bitStream = GetNybbleFromPixelChannels(currPixel, bitStream);

                    dataCtr++;

                    if (dataCtr > ((dataLen / 4) - 1))
                    {
                        x = img.Width;
                        y = img.Height;
                    }
                }
            }

            var hiddenImage = BinaryToByteArray(bitStream);

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllBytes(sfd.FileName, hiddenImage);
            }
        }

        /// <summary>
        /// Retrieve each bit previously stored in each pixel channel
        /// There should be 4 bits (A nybble) retrieved
        /// </summary>
        /// <param name="currPixel">The pixel containing the hidden bits</param>
        /// <param name="bitStream">The variable used to store the retrieved bits</param>
        /// <returns>Returns the bits retrieved</returns>
        private static string GetNybbleFromPixelChannels(Color currPixel, string bitStream)
        {
            // Alpha
            if (currPixel.A % 2 == 1)
            {
                bitStream += 1;
            }
            else
            {
                bitStream += 0;
            }

            // Red
            if (currPixel.R % 2 == 1)
            {
                bitStream += 1;
            }
            else
            {
                bitStream += 0;
            }

            // Green
            if (currPixel.G % 2 == 1)
            {
                bitStream += 1;
            }
            else
            {
                bitStream += 0;
            }

            // Blue
            if (currPixel.B % 2 == 1)
            {
                bitStream += 1;
            }
            else
            {
                bitStream += 0;
            }

            return bitStream;
        }
        #endregion

        #region #### Binary conversions ####
        /// <summary>
        /// Convert binary to a Byte array
        /// </summary>
        /// <param name="data">A string containing binary</param>
        /// <returns>Returns a byte array</returns>
        public static byte[] BinaryToByteArray(string data)
        {
            var bytes = new byte[data.Length / 8];
            int idx = 0;

            for (int i = 0; i < data.Length; i += 8)
            {
                bytes[idx++] = Convert.ToByte(data.Substring(i, 8), 2);
            }

            return bytes;
        }

        /// <summary>
        /// Convert a byte array to binary
        /// </summary>
        /// <param name="data">An array of bytes</param>
        /// <returns>Returns a string containing binary</returns>
        public static string ByteArrayToBinary(byte[] data)
        {
            var buf = new StringBuilder();

            foreach (var b in data)
            {
                var binaryStr = Convert.ToString(b, 2);
                var padStr = binaryStr.PadLeft(8, '0');
                buf.Append(padStr);
            }

            return buf.ToString();
        }
        #endregion
    }
