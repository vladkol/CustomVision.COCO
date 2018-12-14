using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using COCOAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Cognitive.CustomVision.Helpers.COCO
{
    public class CustomVisionProjectTraning
    {
        public ProjectTraningConfiguration configuration
        {
            get;
            private set;
        }

        private CustomVisionProjectTraning()
        {

        }

        public CustomVisionProjectTraning(ProjectTraningConfiguration config)
        {
            configuration = config;
        }


        public async Task<int> LoadWithCOCO(COCODatasetFactory imageSet, bool isDetectionModel)
        {
            var imageUrlBatches = new List<List<ImageUrlCreateEntry>>();
            var imageUrlEntries = new List<ImageUrlCreateEntry>();
            var tagsInBatch = new List<Guid>();
            Dictionary<int, Guid> categoriesToTags = new Dictionary<int, Guid>();

            int imagesIgnored = 0;

            foreach (var cat in imageSet.categoriesAndIds.Keys)
            {
                string name = imageSet.categoriesAndIds[cat];
                var tags = configuration.tagList.Where(tag => tag.Name == name);
                if (tags.Any())
                {
                    categoriesToTags.Add(cat, tags.First().Id);
                }
                else
                {
                    var tag = await configuration.AddTagAsync(name);
                    categoriesToTags.Add(cat, tag.Id);
                }
            }

            for (int i=0; i < imageSet.traningSet.Values.Count; i++)
            {
                var image = imageSet.traningSet.Values.ElementAt(i);

                List<Region> regions = null;
                IList<Guid> tags = null;

                if (isDetectionModel)
                {
                    regions = new List<Region>();
                    foreach (var bb in image.bounding_boxes)
                    {
                        Region region = new Region();
                        region.TagId = categoriesToTags[bb.category_id];

                        if(!tagsInBatch.Contains(region.TagId))
                        {
                            tagsInBatch.Add(region.TagId);
                        }

                        // normalized bounding box  
                        region.Left = bb.bboxArray[0] / image.imageWidth;
                        region.Width = bb.bboxArray[2] / image.imageWidth;
                        region.Top = bb.bboxArray[1] / image.imageHeight;
                        region.Height = bb.bboxArray[3] / image.imageHeight;

                        //x1, x2, y1, y2
                        //[bb[0], bb[0]+bb[2], bb[1], bb[1]+bb[3]]
                        regions.Add(region);
                    }


                    if (regions.Count == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    tags = image.bounding_boxes.Select(bb => categoriesToTags[bb.category_id]).ToList();
                    if(tags.Count == 0)
                    {
                        continue;
                    }

                    foreach (var t in tags)
                    {
                        if (!tagsInBatch.Contains(t))
                        {
                            tagsInBatch.Add(t);
                        }
                    }
                }

                // Traning batch cannot have more than 20 different tags (across all regions)
                bool tooManyTags = false;
                if(tagsInBatch.Count > 19)
                {
                    //If more than 20 tags, "rewind" one image back, we will add this image into the next batch 
                    i--;
                    tooManyTags = true;
                }

                if (!tooManyTags) // if not too many tags with this image included, add this image 
                {
                    ImageUrlCreateEntry entry =
                        isDetectionModel ? new ImageUrlCreateEntry(image.coco_url, null, regions) :
                                            new ImageUrlCreateEntry(image.coco_url, tags);
                    imageUrlEntries.Add(entry);
                }

                // 64 is maximim batch size for Custom Vision, 20 is maximum number of distinct tags per batch 
                // If exceeds, add this batch to the list, and start a new batch 
                if (imageUrlEntries.Count > 63 || tooManyTags) 
                {
                    imageUrlBatches.Add(imageUrlEntries);

                    tagsInBatch.Clear();
                    imageUrlEntries = new List<ImageUrlCreateEntry>();
                }
            }


            if(imageUrlEntries.Count > 0)
                imageUrlBatches.Add(imageUrlEntries);

            foreach (var batch in imageUrlBatches)
            {
                if (batch.Count == 0)
                    continue;

                var createImagesResult = await configuration.trainingApi.CreateImagesFromUrlsWithHttpMessagesAsync(configuration.project.Id,
                    new ImageUrlCreateBatch(batch));

                if (createImagesResult.Response.IsSuccessStatusCode)
                {
                    if (!createImagesResult.Body.IsBatchSuccessful)
                    {
                        //Maybe it's actually OK or we submitted duplicates (OKDuplicate Status) 
                        var notOKImages = createImagesResult.Body.Images.Where(im => (im.Status != "OKDuplicate" && im.Status != "OK" 
                                                                                            && im.Status != "ErrorTagLimitExceed")); // Custom Vision may decline an image with too many regions, but reports it as too many tags
                        if (notOKImages != null && notOKImages.Count() > 0)
                        {
                            var message = await createImagesResult.Response.Content.ReadAsStringAsync();
                            throw new Exception($"Failed to create a trainig image batch (batch is not successful). Result:\n {message}");
                        }
                        else
                        {
                            var tooManyTags = createImagesResult.Body.Images.Where(im => (im.Status == "ErrorTagLimitExceed"));
                            if (tooManyTags != null)
                                imagesIgnored += tooManyTags.Count();
                        }
                    }
                }
                else
                {
                    var message = createImagesResult.Response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create a trainig image batch ({createImagesResult.Response.ReasonPhrase}).");
                }
            }

            return imagesIgnored;
        }

        public async Task Train()
        {
            var traininStartResult = await configuration.trainingApi.TrainProjectWithHttpMessagesAsync(configuration.project.Id);
            if(traininStartResult.Response.IsSuccessStatusCode)
            {
                Iteration iteration = traininStartResult.Body;
                while (iteration.Status != "Completed")
                {
                    await Task.Delay(3000);

                    // Re-query the iteration to get its updated status 
                    var reIterationStatus = await configuration.trainingApi.GetIterationWithHttpMessagesAsync(configuration.project.Id, iteration.Id);
                    if(reIterationStatus.Response.IsSuccessStatusCode)
                    {
                        iteration = reIterationStatus.Body;
                    }
                    else
                    {
                        throw new Exception("Failed to train project (" + reIterationStatus.Response.ReasonPhrase + ")");
                    }
                }
            }
            else
            {
                throw new Exception("Failed to start training project (" + traininStartResult.Response.ReasonPhrase + ")");
            }
        }
    }
}
