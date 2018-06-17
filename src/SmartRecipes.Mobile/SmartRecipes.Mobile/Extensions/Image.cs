﻿using System;
using FFImageLoading.Forms;
using FFImageLoading.Transformations;
using Xamarin.Forms;

namespace SmartRecipes.Mobile.Extensions
{
    public static class Image
    {
        public static CachedImage Thumbnail(Uri source)
        {
            var thumbnail = new CachedImage
            {
                HeightRequest = 32,
                WidthRequest = 32,
                VerticalOptions = LayoutOptions.Center,
                Source = source,
                DownsampleToViewSize = true
            };
            return thumbnail.Tee(t => t.Transformations.Add(new CircleTransformation()));
        }
    }
}
