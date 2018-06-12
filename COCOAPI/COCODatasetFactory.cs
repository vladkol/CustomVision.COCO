using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace COCOAPI
{
    public class COCOImageSetBoundingBox
    {
        public long id;
        public long image_id;
        public int category_id;
        public double[] bboxArray;
    }

    public class COCOImageSetDataItem
    {
        public long image_id;
        public string file_name;
        public string coco_url;
        public double imageWidth;
        public double imageHeight;

        public List<COCOImageSetBoundingBox> bounding_boxes = new List<COCOImageSetBoundingBox>();

    }

    public class COCODatasetFactory
    {
        public Dictionary<long, COCOImageSetDataItem> traningSet
        {
            get;
            private set;
        }

        public IList<string> categories
        {
            get;
            private set;
        }

        public uint maxImageCount
        {
            get;
            private set;
        }

        public Dictionary<int, string> categoriesAndIds
        {
            get;
            private set;
        }

        private COCODatasetFactory()
        {

        }

        public static async Task<COCODatasetFactory> LoadFromCOCOAnnotationJSONFileAsync(string filePathOrUri, IList<string> categoriesToUse, uint maxImageCount)
        {
            if (string.IsNullOrEmpty(filePathOrUri))
            {
                throw new ArgumentException("filePathOrUri cannot be null or empty", filePathOrUri);
            }

            COCODatasetFactory result = new COCODatasetFactory();

            result.maxImageCount = maxImageCount;
            result.categoriesAndIds = new Dictionary<int, string>();

            COCODataSet dataSet = null;

            Uri uri;
            if(Uri.TryCreate(filePathOrUri, UriKind.Absolute, out uri) && 
               !string.IsNullOrEmpty(uri.Host))
            {
                using (WebClient client = new WebClient())
                using (Stream stream = client.OpenRead(filePathOrUri))
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    dataSet = await result.LoadJson(streamReader);
                }
            }
            else
            {
                using (FileStream s = File.Open(filePathOrUri, FileMode.Open))
                using (StreamReader streamReader = new StreamReader(s))
                {
                    dataSet = await result.LoadJson(streamReader);
                }

            }

            IList<string> categories = categoriesToUse;
            if (categories == null || categories.Count == 0)
                categories = dataSet.Categories.Select(c => c.Name).ToList();

#if DEBUG
            Console.WriteLine("\n** Dataset categories: ");
            foreach (var cc in dataSet.Categories)
            {
                Console.WriteLine("{0}", cc.Name);
            }

            Console.WriteLine("\n** Dataset supercategories: ");
            foreach (var sc in dataSet.Categories.Select(c => c.Supercategory).Distinct())
            {
                Console.WriteLine("{0}", sc);
            }
#endif 

            result.maxImageCount = Math.Min(result.maxImageCount, (uint)dataSet.Images.Count);

            if (result.maxImageCount > 0 && categories.Count * 15 > result.maxImageCount) // We need at least 15 images per tag in Custom Vision 
            {
                throw new ArgumentException("maxImageCount should be at least 15 times of number of categories");
            }

            result.categories = categories;

            result.PrepareTrainingSet(dataSet);

            return result;
        }

        private Task<COCODataSet> LoadJson(StreamReader streamReader)
        {
            return Task<COCODataSet>.Run(() =>
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Auto;
                settings.MissingMemberHandling = MissingMemberHandling.Ignore;
                settings.FloatParseHandling = FloatParseHandling.Double;

                JsonSerializer serializer = JsonSerializer.Create(settings);
                COCODataSet data = new COCODataSet();

                using (JsonReader jsonReader = new JsonTextReader(streamReader))
                {
                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType == JsonToken.StartObject)
                        {
                            string path = jsonReader.Path;
                            if (string.IsNullOrEmpty(path))
                                continue;

                            if (path == "info")
                            {
                                data.Info = serializer.Deserialize<COCODataSet.Info2>(jsonReader);
                            }
                            else if (path.StartsWith("licenses["))
                            {
                                if (data.Licenses == null)
                                {
                                    data.Licenses = new List<COCODataSet.License>();
                                }
                                data.Licenses.Add(serializer.Deserialize<COCODataSet.License>(jsonReader));
                            }
                            if (path.StartsWith("images["))
                            {
                                if (data.Images == null)
                                {
                                    data.Images = new List<COCODataSet.Image>();
                                }
                                data.Images.Add(serializer.Deserialize<COCODataSet.Image>(jsonReader)); 
                            }
                            else if (path.StartsWith("annotations["))
                            {
                                if (data.Annotations == null)
                                {
                                    data.Annotations = new List<COCODataSet.Annotation>();
                                }

                                var obj = serializer.Deserialize(jsonReader) as JObject;
                                var seg = obj["segmentation"];
                                if(seg.Type == JTokenType.Array)
                                {                                   
                                    data.Annotations.Add(obj.ToObject<COCODataSet.Annotation1>());
                                }
                                else
                                {
                                    data.Annotations.Add(obj.ToObject<COCODataSet.Annotation2>());
                                }

                            }
                            else if (path.StartsWith("categories["))
                            {
                                if (data.Categories == null)
                                {
                                    data.Categories = new List<COCODataSet.Category>();
                                }
                                data.Categories.Add(serializer.Deserialize<COCODataSet.Category>(jsonReader));
                            }
                        }
                    }

                    return data;
                }

            });
        }


        private void PrepareTrainingSet(COCODataSet data)
        {
            traningSet = new Dictionary<long, COCOImageSetDataItem>();
            Random rnd = new Random();
            List<long> imagesToUse = new List<long>();

            uint realMaxImageCount = maxImageCount > 0 ? maxImageCount : (uint)data.Images.Count;

            // Only categories we want to have 
            var selectedCategories = data.Categories.Where(c => categories.Contains(c.Name) || categories.Contains(c.Supercategory));
            var selectedCategoriesIds = selectedCategories.Select(sc => sc.Id);
            Dictionary<int, List<long>> categoriesImages = new Dictionary<int, List<long>>();
            Dictionary<int, int> categoriesIndexes = new Dictionary<int, int>();

            foreach (var category in selectedCategories)
            {
                List<COCODataSet.Annotation> catAnnotations = new List<COCODataSet.Annotation>();
                foreach (var a in data.Annotations)
                {
                    if (a.CategoryId == category.Id && a.Bbox != null && a.Bbox.Length > 0)
                        catAnnotations.Add(a);
                }

                categoriesAndIds.Add(category.Id, category.Name);

                var catImages = catAnnotations.Select(ca => ca.ImageId).ToList();
                List<long> catUniqueImages = new List<long>();

                foreach (var ca in catImages)
                {
                    if (!catUniqueImages.Contains(ca))
                    {
                        catUniqueImages.Add(ca);
                    }
                }

                categoriesImages.Add(category.Id, catUniqueImages);
                categoriesIndexes.Add(category.Id, 0);
            }

            bool addedImage = false;
            do
            {
                foreach (var catId in categoriesImages.Keys)
                {
                    if (imagesToUse.Count == realMaxImageCount)
                        break;

                    var catImages = categoriesImages[catId];
                    bool foundNewCatImage = false;
                    do
                    {
                        if (categoriesIndexes[catId] == catImages.Count)
                            break;

                        var catImage = catImages[categoriesIndexes[catId]];

                        if (!imagesToUse.Contains(catImage))
                        {
                            imagesToUse.Add(catImage);
                            addedImage = true;
                            foundNewCatImage = true;
                        }
                        categoriesIndexes[catId]++;

                    }
                    while (!foundNewCatImage);
                }
            }
            while (addedImage && imagesToUse.Count < realMaxImageCount);

            int maxUseCnt = imagesToUse.Count;

            foreach (var imageId in imagesToUse)
            {
                COCOImageSetDataItem item = null;
                bool existing = false;

                if (traningSet.ContainsKey(imageId))
                {
                    maxUseCnt--;
                    item = traningSet[imageId];
                    existing = true;
                }

                if (item == null)
                {
                    item = new COCOImageSetDataItem();
                    item.image_id = imageId;
                }

                var imageAnnotations = data.Annotations.Where(a => a.ImageId == imageId).ToList();

                foreach (var ia in imageAnnotations)
                {
                    if (!selectedCategoriesIds.Contains(ia.CategoryId))
                        continue;

                    COCOImageSetBoundingBox bbox = new COCOImageSetBoundingBox();
                    bbox.id = ia.Id;
                    bbox.bboxArray = ia.Bbox;
                    bbox.category_id = ia.CategoryId;
                    bbox.image_id = ia.ImageId;
                    item.bounding_boxes.Add(bbox);
                }

                if (item.bounding_boxes.Count == 0)
                {
                    maxUseCnt--;
                    continue;
                }

                if (!existing)
                {
                    traningSet.Add(item.image_id, item);
                }

                if (traningSet.Count == maxUseCnt)
                {
                    break;
                }
            }


            foreach (var item in traningSet.Values)
            {
                var image = data.Images.Where(i => i.Id == item.image_id).First();
                item.file_name = image.FileName;
                item.coco_url = image.CocoUrl;
                item.imageWidth = image.Width;
                item.imageHeight = image.Height;
            }
        }

        public static async Task<COCODatasetFactory> LoadFromCOCOAnnotationJSONFileAsync(string filePathOrUri, string category, uint numberOfCatergoryImages)
        {
            List<string> categories = new List<string>();
            categories.Add(category);
            return await LoadFromCOCOAnnotationJSONFileAsync(filePathOrUri, categories, numberOfCatergoryImages);
        }
    }
}
