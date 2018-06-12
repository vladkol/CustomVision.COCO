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


        public async Task LoadWithCOCO(COCODatasetFactory imageSet, bool isDetectionModel)
        {
            var imageUrlBatches = new List<List<ImageUrlCreateEntry>>();
            var imageUrlEntries = new List<ImageUrlCreateEntry>();
            Dictionary<int, Guid> categoriesToTags = new Dictionary<int, Guid>();

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

            foreach (var image in imageSet.traningSet.Values)
            {
                List<Region> regions = null;
                IList<Guid> tags = null;

                if (isDetectionModel)
                {
                    regions = new List<Region>();
                    foreach (var bb in image.bounding_boxes)
                    {
                        Region region = new Region();
                        region.TagId = categoriesToTags[bb.category_id];

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
                }
                

                ImageUrlCreateEntry entry = 
                    isDetectionModel ? new ImageUrlCreateEntry(image.coco_url, null, regions) : 
                                        new ImageUrlCreateEntry(image.coco_url, tags);
                imageUrlEntries.Add(entry);

                if (imageUrlEntries.Count > 63) // 64 is maximim batch size for Custom Vision 
                {
                    imageUrlBatches.Add(imageUrlEntries);
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
                        throw new Exception("Failed to create a trainig image batch (batch is not successful)");
                    }
                }
                else
                {
                    throw new Exception("Failed to create a trainig image batch (" + createImagesResult.Response.ReasonPhrase + ")");
                }
            }
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
