# OsMapDownloader
Downloads Ordinance Survey maps and exports them to the .QCT file format. To download, go to the [Releases](https://github.com/piggeywig2000/OsMapDownloader/releases) page.

## About this program
The .QCT file format is a file format created by Memory-Map to store large high resolution maps. QCT maps can be read by [official Memory-Map apps](https://memory-map.com/downloads/) (available on iOS, Android, Windows, Mac, and Linux) as well as some other apps not made by Memory-Map, such as [AlpineQuest](https://alpinequest.net/) on Android.

This program creates a QCT map of a given area by downloading the mapping data directly from Ordinance Survey's servers, ensuring that the map is up to date when the map is downloaded.

### Versions

Normal users should use the GUI version (called `OsMapDownloader.Gui.win-x64.zip` on the [Releases](https://github.com/piggeywig2000/OsMapDownloader/releases) page), however this is only available for Windows.

There are also CLI versions available for Windows, Mac, and Linux for users who are comfortable with using the command line.

### Copyright
This program should only be used by users who have an [OS maps subscription](https://shop.ordnancesurvey.co.uk/apps/os-maps-subscriptions/) but would rather use another app to view their maps instead of the Ordinance Survey app. The use of this program without a valid subscription is a violation of copyright law.

## Screenshots
![Screenshot of software showing OS map of Arncliffe with the map border overlayed](https://user-images.githubusercontent.com/32236823/209836623-14163195-78fd-4c21-a73c-cc7efbb8cd28.png)
![Screenshot of software exporting a map of Littondale](https://user-images.githubusercontent.com/32236823/209837532-f7101355-9fcc-49e4-825c-4c3107d78a43.png)

## Command Line Documentation
There are two modes, `box` and `points`:
- `box` creates a rectangular map by specifying the top left and bottom right corners of the map.
- `points` creates a map of any shape by definining a series of points in a separate text file, which are joined together to make the map border.

```
USAGE:
box <TopLeftLatitude> <TopLeftLongitude> <BottomRightLatitude> <BottomRightLongitude> <FilePath> [options...]
points <BorderPointsPath> <FilePath> [options...]

  box        Create a rectangular map by defining the top left corner and bottom right corner of the map
  points     Create a map of any shape by defining the border of a map as a series of points, which are joined together clockwise.
             The file should contain a list of points separated by commas
  help       Display more information on a specific command.
  version    Display version information.
```

### Options
Both box and points mode accept the following optional arguments:
```
  -s, --silent                     (Default: false) Do not write anything to the console
  -q, --quiet                      (Default: false) Only write errors to the console
  -d, --debug                      (Default: false) Write some more information to the console. Useful for debugging
  -v, --verbose                    (Default: false) Write a lot of information to the console. Useful for debugging
  -o, --overwrite                  (Default: false) If the file exists, overwrite it
  --scale                          (Default: 1:25000) The scale of the map to download. Either 1:25000, 1:50000, 1:250000,
                                   or 1:1000000
  --polynomial-sample-size         (Default: 2500) The number of rows and columns in the grid of samples taken when calculating
                                   the polynomial coefficients for GPS coordinate transformations.
                                   Increase this for a higher GPS accuracy, decrease this for lower memory usage and a
                                   faster processing time
  --token                          The token to use when downloading tiles. By default the program will try to fetch this
                                   automatically, but you can manually specify it with this option if that doesn't work
  --disable-hw-accel               (Default: false) Tiles are processed on the CPU instead of the GPU. It will reduce
                                   processing speed, so keep this disabled unless you're having issues
  --keep-tiles                     (Default: false) Don't delete the downloaded tiles after completion
  --long-name                      A longer version of the map's name
  --name                           The map's name
  --identifier                     Metadata thing, but I have no idea what it is. Seems to be used to identify a specific place,
                                   such as an airport
  --edition                        The map edition. Usually a year
  --revision                       The map revision. Usually a number; 1 if it's the first revision
  --keywords                       Metadata thing. I have never seen this used
  --copyright                      Copyright information
  --custom-scale                   The map scale. Defaults to a value based on the scale
  --datum                          (Default: WGS84) The datum used
  --depths                         (Default: Meters) The depth units
  --heights                        (Default: Meters) The height units
  --projection                     (Default: UTM) The projection used
  --type                           The map type. Defaults to Land for Explorer and Landranger scales, and Road for Road and
                                   MiniScale scales
  --help                           Display this help screen.
  --version                        Display version information.
  TopLeftLatitude (pos. 0)         Required. The latitude of the top left corner of the bounding box
  TopLeftLongitude (pos. 1)        Required. The longitude of the top left corner of the bounding box
  BottomRightLatitude (pos. 2)     Required. The latitude of the bottom right corner of the bounding box
  BottomRightLongitude (pos. 3)    Required. The longitude of the bottom right corner of the bounding box
  FilePath (pos. 4)                Required. The path to where the file should be saved. Can be a relative or absolute path
```

### Examples
`box 53.985008 -1.161702 53.981299 -1.140141 "area.qct" -o --name="York" --edition="2015"`<br />
Download the city of York, saving the file with name area.qct in the current folder. Overwrite the file if it already exists.<br />
Metadata tags: Name is York, Edition is 2015.

`box 53.125098 -4.136918 53.114719 -4.110482 "maps/holiday.qct" -quiet --name="Summer Holiday 2020" --long-name="Summer Holiday 2020 destination" --revision="1"`<br />
Download the village of Llanberis, saving the file with name holiday.qct in a folder called maps in the current folder. Only output error messages.<br />
Metadata tags: Name is Summer Holiday 2020, Long Name is Summer Holiday 2020 destination, Revision is 1.

`points "border.txt" "map.qct" --scale="1:50000" --name="Complex Map"`<br />
Download a map with a border defined by latitude-longitude pairs found in border.txt, connected together.<br />
The map is at 1:50000 scale (instead of the default 1:25000).

The border.txt file is a series of latitude-longitude coordinates that, when connected, define the border. For example:
```
54.206533026289335, -2.231469322015471
54.206635359837385, -2.1701450835141145
54.12586582743817, -2.0321015971629777
54.10789117236023, -2.032087702922439
54.10783140280742, -2.1238557383971965
54.17058378154476, -2.2312686068378866
```
