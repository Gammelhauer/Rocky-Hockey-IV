﻿using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using RockyHockey.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RockyHockey.MotionCaptureFramework
{
    public class PositionCalculator
    {
        public static Task<TimedCoordinate> ProcessImage(TimedImage image, bool sliceImage = true)
        {
            return Task<TimedCoordinate>.Factory.StartNew(() =>
            {
                TimedCoordinate retval = null;

                if (image.image.Size != new Size(0, 0))
                {
                    try
                    {
                        Mat greenHueImage = new Mat();
                        Mat hsvImage = new Mat();

                        Mat slicedMat;

                        if (sliceImage)
                            slicedMat = SliceSingleMat(image.image);
                        else
                            slicedMat = image.image;

                        //removes noise from image (pixels that don't fit into their surrounding)
                        CvInvoke.MedianBlur(slicedMat, slicedMat, 3);

                        CvInvoke.CvtColor(slicedMat, hsvImage, ColorConversion.Bgr2Hsv);

                        // Threshold the HSV image, keep only the red pixels
                        CvInvoke.InRange(hsvImage, new ScalarArray(new MCvScalar(40, 33, 30)), new ScalarArray(new MCvScalar(85, 255, 255)), greenHueImage);

                        // Make the image blurry
                        CvInvoke.GaussianBlur(greenHueImage, greenHueImage, new Size(9, 9), 2, 2);

                        // Use the Hough transform to detect circles in the combined threshold image
                        Mat grayScaledImage = greenHueImage.Split().FirstOrDefault();

                        //https://docs.opencv.org/3.0-beta/doc/py_tutorials/py_imgproc/py_houghlines/py_houghlines.html
                        Mat edges = new Mat();
                        CvInvoke.Canny(grayScaledImage, edges, 50, 150);

                        CircleF[] circles = CvInvoke.HoughCircles(edges, HoughType.Gradient, 1, greenHueImage.Rows / 8, 100, 20, 5, 10);

                        if (circles.Any())
                        {
                            var circle = circles.First();

                            if (circle.Center.X != 0 && circle.Center.Y != 0)
                                retval = new TimedCoordinate
                                {
                                    X = circle.Center.X,
                                    Y = circle.Center.Y,
                                    Timestamp = image.timeStamp
                                };
                        }
                    }
                    catch (Exception ex)
                    {
                        TextFileLogger.Instance.Log(ex);
                    }
                }

                return retval;
            });
        }

        /// <summary>
        /// transforms distorted gamefield on the picture into a rectangle
        /// </summary>
        /// <param name="mat">input image</param>
        /// <returns></returns>
        private static Mat SliceSingleMat(Mat mat)
        {
            var config = Config.Instance;

            var sourcePointsMat = new PointF[4];
            sourcePointsMat[0] = new PointF(config.Camera1.UpperLeft.X, config.Camera1.UpperLeft.Y);
            sourcePointsMat[1] = new PointF(config.Camera1.UpperRight.X, config.Camera1.UpperLeft.Y);
            sourcePointsMat[2] = new PointF(config.Camera1.LowerLeft.X, config.Camera1.UpperLeft.Y);
            sourcePointsMat[3] = new PointF(config.Camera1.LowerRight.X, config.Camera1.UpperLeft.Y);

            var destinationPointsMat = new PointF[4];
            destinationPointsMat[0] = new PointF(0, 0);
            destinationPointsMat[1] = new PointF(320, 0);
            destinationPointsMat[2] = new PointF(0, 240);
            destinationPointsMat[3] = new PointF(320, 240);

            var rectangle = new Rectangle(config.Camera1.Offset.FromLeft, config.Camera1.Offset.FromTop, config.Camera1.FieldSize.Width, config.Camera1.FieldSize.Height);
            Mat lambda = CvInvoke.GetPerspectiveTransform(sourcePointsMat, destinationPointsMat);
            CvInvoke.WarpPerspective(mat, mat, lambda, new Size(320, 240));
            Mat cropped = new Mat(mat, rectangle);

            return cropped;
        }
    }
}