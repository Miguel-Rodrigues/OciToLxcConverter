# OciToLxcConverter

A small console app that converts OCI archives from Buildah into LXC rootfs images.

## Background
I created this console app to help the process of automating the build of OCI images from docker hub into Linux container rootfs images, specifically for deploying Linux Containers into my Proxmox VE Homelab Cluster using Buildah and Terraform.

Buildah when deploys OCI archives returns a TAR file that is not quite compatible with the template format that proxmox VE expects for their CTs. Proxmox VE requires that each CT template archive is a direct rootfs image. In an OCI archive, this image is broken down into multiple layers, one per each image transformation, and burrowed down in the `blob` folder, as a gzip files, with generic unique identifier names.

To solve this issue I have to first build the image, with the squash flag enabled and export it with gzip compression into an OCI archive. 

## Usage:
`OciToLxcConverter <source tar file> [<destination folder>]`

Assuming the Following dockerfile. (doesn't matter if the entrypoint is wrong, just for example...)

```dockerfile
FROM alpine:latest
RUN apk add --no-cache mysql-client
ENTRYPOINT mysql "hello" 12345
```

This docker file loads the latest alpine image, installs mySQL client and runs the entrypoint as `mysql "hello" 12345`

We now build the dockerfile as follows:
```sh
$ buildah bud --squash -t mysqlcustom:latest ./dockerfile
```

And export the image as a OCI gzip archive
```sh
$ buildah --compression-format gzip --compression-level 9 push mysqlcustom:latest oci-archive:mysqlcustom.tar
```

This creates a tar archive with the following structure:
```
mysqlcustom.tar
- index.json (1KB)
- oci-layout (1KB)
- blobs/sha256
  - 3920b2ffa64ac6e1d6672c3ce3c021cbcc0ee7741005be8deea1eea8345c2dc1 (1KB)
  - adb3d79574fb952943d4c10640175b957ad8ee832f8afbbca118ddeee7e9dc87 (1KB)
  - f9afcdbf3e6d52cac9b355bdbae788dac0ec1feeb0488d41d18b292f2c30679c (24KB)
```

As you can see the archive does not says much about where the rootfs is stored, except a hint from the file size. here is where the console app enters. It will look to the `index.json` that points to the manifest json file in the `blobs/sha254` folder.

The manifest file contains two properties. The `config` and `layers`. The `config` points to the dockerfile environment variables and entrypoint settings. The `layers` points to all transformation iterations that the image was subjected. Since the squash flag was applied, there is only one layer and thus one rootfs.

With this I create two files. the Entrypoint.sh and the `.tar.gz` file with the same name of the original package. Then the files are saved either on the current directory or a specified directory, removing the used original archive.

```sh
$ ./OciToLxcConverter ./mysqlcustom.tar
```
```
Checking archive
Extract archive
look for index.json
Look for entrypoint script and image archive
Saving entrypoint script and image
Garbage collecting
All done :)
```
Resulting into the following files:
```sh
$ ls -alh
```
```
total 87M
drwxr-xr-x  2 root root   4.0K Aug 23 19:04 .
drwx------ 10 root daemon 4.0K Aug 23 18:16 ..
-rwxrwxr-x  1 root root    64M Aug 23 18:36 OciToLxcConverter
-rw-r--r--  1 root root     86 Aug 23 18:37 dockerfile
-rw-r--r--  1 root root     30 Aug 23 18:38 entrypoint.sh
-rw-r--r--  1 root root    23M Aug 23 18:38 mysqlcustom.tar.gz
```
```sh
$ cat ./entrypoint.sh
```
```
/bin/sh -c mysql "hello" 12345
```

This gzip file can then be uploaded to the defined template folder on the proxmox ve and the entrypoint can be assigned to a settings `lxc.init` on the container config file. (To be tested)

## TODO:
- Extract the environment variables into a file
- Find a way to inject the environment variables directly to the image rootfs or into the CT config file.
- Find a way to inject the entrypoint script directly to the image rootfs and run after startup, or into the CT config file.