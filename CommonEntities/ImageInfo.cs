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

using OMV = OpenMetaverse;

using org.herbal3d.cs.CommonEntitiesUtil;

namespace org.herbal3d.cs.os.CommonEntities {

    public class ImageInfo {
        public EntityHandle handle;
        public OMV.UUID imageIdentifier;
        public bool hasTransprency = false;
        public bool resizable = true;   // true if image can be reduced in size
        public PersistRules persist;    // information in filesystem storage of the image
        public Image image = null;
        public int xSize = 0;
        public int ySize = 0;

#pragma warning disable 414
        private readonly string _logHeader = "[ImageInfo]";
#pragma warning restore 414
        private readonly BLogger _log;
        private readonly IParameters _params;

        public ImageInfo(BLogger pLog, IParameters pParams) : this(new EntityHandleUUID(), pLog, pParams ){
        }

        public ImageInfo(EntityHandle pHandle, BLogger pLog, IParameters pParams) {
            handle = pHandle;
            imageIdentifier = handle.GetUUID(); // image is unique unless underlying set
            _log = pLog;
            _params = pParams;
            persist = new PersistRules(PersistRules.AssetType.Image, handle.ToString(), pLog, pParams);
        }

        // Create a new ImageInfo that has a copy of all the information from this one.
        // THis creates a copy of the image so it can be modified without touching the original.
        public ImageInfo Clone() {
            ImageInfo ret = new ImageInfo(_log, _params);
            if (image != null) {
                ret.SetImage((Image)image.Clone());
            }
            return ret;
        }

        // Set the image into this structure and update all the auxillery info
        public void SetImage(Image pImage) {
            image = pImage;
            xSize = image.Width;
            ySize = image.Height;
            hasTransprency = CheckForTransparency();
            if (hasTransprency) {
                persist = new PersistRules(PersistRules.AssetType.ImageTrans, handle.ToString(), _log, _params);
            }
            else {
                persist = new PersistRules(PersistRules.AssetType.Image, handle.ToString(), _log, _params);
            }
            // _log.DebugFormat("{0} SetImage. ID={1}, xSize={2}, ySize={3}, hasTrans={4}",
            //             _logHeader, handle, xSize, ySize, hasTransprency);
        }

        // The hash code for an image is just the hash of its UUID handle.
        public BHash GetBHash() {
            return handle.GetBHash();
        }

        // Check the image in this TextureInfo for transparency and set this.hasTransparency.
        public bool CheckForTransparency() {
            hasTransprency = false;
            if (image != null) {
                if (Image.IsAlphaPixelFormat(image.PixelFormat)) {
                    // The image could have alpha values in it
                    if (image is Bitmap bitmapImage) {
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
            if (image != null && (image.Width > size || image.Height > size)) {
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
                xSize = thumbNail.Width;
                ySize = thumbNail.Height;
                ret = true;
            }
            return ret;
        }

        public override string ToString()
        {
            return String.Format("id={0},{1}x{2}{3}", handle.GetUUID(),
                            xSize, ySize,
                            hasTransprency ? "/hasTrans" : "");
        }


    }

}
