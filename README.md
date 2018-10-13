# Libmmd-for-Unity

Libmmd-for-Unity is a Unity version of [libmmd](https://github.com/itsuhane/libmmd), which is for loading mikumikudance files at **runtime** in Unity 3D.

## Features

- support pmd, pmx 2.0, vmd and vpd file.
- support png, jpg, bmp, tga and dds format texture files.
- use Bullet Physics for physics calculation.
- async physics calculation, which improve the performance.
- support pre-calculation of physics. The result of bone motion can be saved to a file, which can be used to play the motion with the pre-calculated result.

## Usage

### Run demos

- Download the source code. 
- Play the scenes under the folder Assets/LibMmdDemo. You need to fill your mikumikudance model， motion and camera motion path to the component under the "GameController" object before playing.

### Use in your project

- Copy the "Libmmd" directory and the "msp.rsp" file to your project.
- Copy the I18N.CJK.dll file to your plugin folder in your project. This file is for decoding Shift-JIS strings in mikumikudance files. You can find the file under your Unity 3D installation directory. 

## License

Libmmd-for-Unity is free software available under the 3-Clause BSD License. See the file "LICENSE" for license conditions.

3rd party software licenses are under the "3rd-party-softwore-licenses" directory.

## 3rd party software

Libmmd-for-Unity makes use of the following 3rd party software:

- mmd-for-unity - Copyright (c) 2011, Eiichi Takebuchi, Takahiro Inoue, Shota Ozaki, Masamitsu Ishikawa, Kazuki Yasufuku, Fumiya Hirano. - provided under the 3-Clause BSD license.

  https://github.com/mmd-for-unity-proj/mmd-for-unity

- BulletSharpUnity3d - Copyright for portions of project BulletSharp and BulletSharpPInvoke are held by Andres Traks, 2013-2015 as part of project BulletSharpUnity3d - provided under the zlib/libpng License

  https://github.com/Phong13/BulletSharpUnity3d

- MMDCameraPath -  Copyright {@2017} { @Rumeng (chinesename liuzhanhao) } email:liuzhanhao96@126.com Licensed under the Apache License, Version 2.0 (the "License")

  https://github.com/lzh1590/MMDCameraPath

