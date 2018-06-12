# [COCO](http://cocodataset.org) API and [Custom Vision](https://customvision.ai) Traning  

Traning Azure [Custom Vision](https://customvision.ai) projects using [COCO](http://cocodataset.org) dataset 

![COCO to Custom Vision](https://user-images.githubusercontent.com/4735184/41110506-6acd7732-6a2e-11e8-91f5-102960be57d4.jpg) 

This sample shows how to train Azure Custom Vision classification (compact) and detection models using COCO dataset images without downloading images to your machine. 

You can use COCOAPI library for converting COCO dataset to other formats, traning and validating your models with any service or Machine Learning framework from .NET. 

## Sample COCOCustomVisionTrainer
**Usage** 
``` 
COCOCustomVisionTrainer [options]
```

### Options:
```
  -tkey | --trainingKey   Provide your Custom Vision training key (https://www.customvision.ai/projects#/settings) as -tkey | --trainingKey YOUR_KEY
  -f | --fileDatasetJSON  Provide a path or URL of a COCO Train/Val annonations JSON file as -f | --fileDatasetJSON instances_train*.json or instances_val*.json. Download them from http://cocodataset.org/#download
  -p | --projectName      Specify -p | --projectName if requres traning as a Detection project
  -d | --detection        Specify if it is a Detection project
  -t | --train            Specify if automatically train the project
  -c | --categories       COCO categories or supercategories to use. If no categories specified, training on all categories
  -i | --images           Maximum number of images across categories -i | --images. If no number specified, using all images with objects of categories.
  -? | -h | --help        Show help information
```

### Important totes  
* Sample supports Detection and General (compact) classification models are supported (see ProjectTraningConfiguration.GetDefaultModel function) 
* Azure Custom Vision and COCOCustomVisionTrainer sample only support segmentation with bounding boxes 
* COCOCustomVisionTrainer sample is configured with 5000 images maximum ([as for free tiers of Custom Vision today](https://docs.microsoft.com/en-us/azure/cognitive-services/custom-vision-service/limits-and-quotas))

### Categories and supercategories from instances_train2014.json 
```
** Individual categories
person
bicycle
car
motorcycle
airplane
bus
train
truck
boat
traffic light
fire hydrant
stop sign
parking meter
bench
bird
cat
dog
horse
sheep
cow
elephant
bear
zebra
giraffe
backpack
umbrella
handbag
tie
suitcase
frisbee
skis
snowboard
sports ball
kite
baseball bat
baseball glove
skateboard
surfboard
tennis racket
bottle
wine glass
cup
fork
knife
spoon
bowl
banana
apple
sandwich
orange
broccoli
carrot
hot dog
pizza
donut
cake
chair
couch
potted plant
bed
dining table
toilet
tv
laptop
mouse
remote
keyboard
cell phone
microwave
oven
toaster
sink
refrigerator
book
clock
vase
scissors
teddy bear
hair drier
toothbrush

** Supercategories
person
vehicle
outdoor
animal
accessory
sports
kitchen
food
furniture
electronic
appliance
indoor
```

