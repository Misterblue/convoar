/*
 * Copyright (c) 2017 Robert Adams
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace org.herbal3d.convoar {

    public class ImageInfo {
        public EntityHandle handle;
        public bool hasTransprency;
        public Image image;
        public int xSize;
        public int ySize;

        public ImageInfo() {
            handle = new EntityHandle();
            hasTransprency = false;
            image = null;
            xSize = ySize = 0;
        }

        public ImageInfo(Image pImage) {
            handle = new EntityHandle();
            hasTransprency = false;
            this.SetImage(pImage);
        }

        // Set the image into this structure and update all the auxillery info
        public void SetImage(Image pImage) {
            image = pImage;
            xSize = image.Width;
            ySize = image.Height;
            this.CheckForTransparency();
        }

        // The hash code for an image is just the hash of its UUID handle.
        public BHash GetHash() {
            return new BHashULong(handle.GetUUID().GetHashCode());
        }

        // Check the image in this TextureInfo for transparency and set this.hasTransparency.
        public bool CheckForTransparency() {
            hasTransprency = false;
            if (image != null) {
                if (Image.IsAlphaPixelFormat(image.PixelFormat)) {
                    // The image could have alpha values in it
                    Bitmap bitmapImage = image as Bitmap;
                    if (bitmapImage != null) {
                        for (int xx = 0; xx < bitmapImage.Width; xx++) {
                            for (int yy = 0; yy < bitmapImage.Height; yy++) {
                                if (bitmapImage.GetPixel(xx, yy).A != 255) {
                                    hasTransprency = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return hasTransprency;
        }

        /// <summary>
        /// If the image is larger than a max, resize the image.
        /// </summary>
        /// <param name="maxTextureSize"></param>
        /// <returns>'true' if the image was converted</returns>
        public bool ConstrainTextureSize(int maxTextureSize) {
            bool ret = false;
            int size = maxTextureSize;
            if (image.Width > size || image.Height > size) {
                int sizeW = size;
                int sizeH = size;
                /*
                if (inImage.Width > size) {
                    sizeH = (int)(inImage.Height * (size / inImage.Width));
                }
                else {
                    sizeW = (int)(inImage.Width * (size / inImage.Height));
                }
                */
                Image thumbNail = new Bitmap(sizeW, sizeH, image.PixelFormat);
                using (Graphics g = Graphics.FromImage(thumbNail)) {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Rectangle rect = new Rectangle(0, 0, sizeW, sizeH);
                    g.DrawImage(image, rect);
                }
                image = thumbNail;
                ret = true;
            }
            return ret;
        }

    }

}
