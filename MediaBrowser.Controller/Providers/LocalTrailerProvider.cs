﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Events;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.Providers
{
    [Export(typeof(BaseMetadataProvider))]
    public class LocalTrailerProvider : BaseMetadataProvider
    {
        public override bool Supports(BaseEntity item)
        {
            return item is BaseItem;
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.First; }
        }

        public async override Task Fetch(BaseEntity item, ItemResolveEventArgs args)
        {
            BaseItem baseItem = item as BaseItem;

            var trailerPath = args.GetFileSystemEntryByName("trailers", true);

            if (trailerPath.HasValue)
            {
                string[] allFiles = Directory.GetFileSystemEntries(trailerPath.Value.Key, "*", SearchOption.TopDirectoryOnly);

                List<Video> localTrailers = new List<Video>();

                for (int i = 0; i < allFiles.Length; i++)
                {
                    string file = allFiles[i];

                    BaseItem child = await Kernel.Instance.ItemController.GetItem(file).ConfigureAwait(false);

                    Video video = child as Video;

                    if (video != null)
                    {
                        localTrailers.Add(video);
                    }
                }

                baseItem.LocalTrailers = localTrailers;
            }
        }
    }
}
