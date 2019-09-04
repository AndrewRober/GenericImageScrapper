#  GenericImageScrapper
a generic parallel image scrapper with support of multiple regex format to extract images link with base64 support

### regex patterns used:
- `"<img(.*?)src=\"(.*?)\""`
- `"(background|background-image):url\\(['\"]?(.*?)['\"]?\\)"`
- `"data:image\\/\\w+;base64,[^='\"&]*[=]*"`

#### External dependencies: 
**ImageSharp**

# Main features
- Validate Urls and convert releative urls to absolute ones
- Attempt to extract original file name using the "Content-Disposition" header
- handle directory and file name creation properly
- download images in parallel using different regex patterns
- support fast and efficient extraction of base64 images embedded in the html

## My TODO list / things that could be done
- add my crawler and get images from the same domain
- navigate through the CSS files to extract images references from there too
- add a simple UI and host it as a free tool
- zip the findings folder to save space or compress and write to archieve directly (depends on the use case)

this was hacked together in 2 hours to help in a different project, could use some development but for now, it does its job. **its simple yet very efficient.**
