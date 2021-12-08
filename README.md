# MultiCache
![example workflow](https://github.com/XorZy/MultiCache/actions/workflows/dotnet.yml/badge.svg)

A modern, feature-packed, yet very to use caching server for Pacman.

Support for other package managers is planned.

## Use cases

* MultiCache is very helpful if you have a slow and/or unreliable internet connection.
* It automatically learns what packages are installed on your machine(s) and then downloads updates automatically.
* You can configure it to download updates at specific times of the day.
You can, for instance, run it in the middle of the night, or during the day but with a maximum download speed.
* Even if you have a decent internet connection, MultiCache can still decrease your upgrade times considerably, particularly if you are running multiple virtual machines or docker containers.

<details><summary>Why not just clone a mirror locally?</summary>
Modern Linux distributions have many, many packages. Cloning an entire repository would download dozens of gigabytes, which is simply not an option if you internet is slow or metered. MultiCache appears to your computers as a regular mirror, except that it only downloads the data that you actually need.
</details>
<details><summary>Why not use cron and upgrade your system periodically?</summary>
This could be a solution if you have only one computer. However, if you have multiple computers then you may end up having to download a given package multiple times. MultiCache makes the process easier by acting like a local mirror.
A typical setup would be to run MultiCache on a low-power device like a Raspberry Pi or any other SBC. This way you don't need to let your other power hungry computers running all day and yet can still update them at the time of your choosing, and in a fraction of the time.
</details>

<details><summary>Why not use Squid?</summary>

Squid can be used as a simple caching proxy but it knows nothing about the packages it stores. MultiCache on the other hand is able to not only cache the current version of a given package, as well as detect and download updates.
</details>

## Features

- Very easy to install and configure
- Very configurable
- Https support
- Out-of-the-box support for Manjaro(both/x64) and ArchLinux(arm/x64)
- Automatic mirror configuration
- Handles concurrent downloads smoothly, i.e if multiple clients request at the same time a file that is not yet cached, the server will only download the data once.
- Enables you to update all your Linux installations at the same exact time, without ever needing to download a package more than once.
- Works on any Linux Distribution (does not depend on Pacman ot other distro-specific packages)

- Automatic integrity check and repair of packages
- And many more...

## Installation

**Please note that this a very early release of the application.**.

Nothing is finalized yet so there may be breaking changes in the file configuration format or file storage layout.
I cannot recommend the app for daily use yet but it would be awesome if you want to help improve the application by discovering bugs or suggesting improvements!

### How to try the application

#### Dependencies
* dotnet-sdk6
* git

1. Clone the repository

```sh
git clone https://github.com/XorZy/MultiCache
cd MultiCache/src/MultiCache/
```
2. Then run the app and follow the setup instructions

```sh
dotnet run
```

3. On all your other computers run the following command as root to use MultiCache as a mirror.

```sh
    echo "Server=http://[ip]:5050/[repository]/\$repo/\$arch" > /etc/pacman.d/mirrorlist
```

1. You can then run pacman as you would normally and MultiCache will learn what packages you use automatically.

2. Optionally you can let MultiCache know immediately about the packages that are installed on your machine(s). Run the following command on every computer you wish to keep updated with MultiCache

```sh
pacman -Q | curl -X POST --data-binary @- http://[ip]:5050/[repository]/api/packages?arch=x86_64
```

### Installation

Installation scripts and AUR PKGBUILD will be provided as soon as the app is stable enough.

If you do not want to compile the application yourself, binaries are automatically produced every time this repository is updated.
Head over to https://github.com/XorZy/MultiCache/actions/workflows/dotnet.yml.
Chose the latest successful run, scroll down to the bottom of the page, the binaries will be in the Artifacts section.
These binaries use AOT compilation instead of the traditional JIT compilation of C#.
This should offer faster startup times.
