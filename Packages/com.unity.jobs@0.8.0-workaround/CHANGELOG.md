# Change log

## [0.8.0] - 2020-11-13

### Changed

* Update minimum editor version to 2020.1.2f1
*Added tests for generic jobs.

## [0.7.0] - 2020-09-24

* Updated dependencies of this package.

## [0.6.0] - 2020-08-26

* Updated dependencies of this package.
## [0.5.0] - 2020-08-04

* Updated dependencies of this package.

## [0.4.0] - 2020-07-10


### Changed

* Updated minimum Unity Editor version to 2020.1.0b15 (40d9420e7de8)

### Known Issues

* This version is not compatible with 2020.2.0a17. Please update to the forthcoming alpha.

## [0.3.0] - 2020-05-27

### Changed

* Updated minimum Unity Editor version to 2020.1.0b9 (9c0aec301c8d)

## [0.2.10] - 2020-05-04

### Changed

* Updated dependencies of this package.


## [0.2.9] - 2020-04-24

### Changed

* Updated dependencies of this package.


## [0.2.8] - 2020-04-08

### Changed

* Updated dependencies of this package.


## [0.2.7] - 2020-03-13

### Changed

* Updated dependencies of this package.
* The internals of IJobParallelForFilter are now `internal` rather than `public`


## [0.2.6] - 2020-03-03

### Changed

* Updated dependencies of this package.
* Maintain JobsDebugger menu item value between sessions.


## [0.2.5] - 2020-02-17

### Changed

* Updated dependencies of this package.


## [0.2.4] - 2020-01-28

### Changed

* Updated dependencies of this package.


## [0.2.3] - 2020-01-16

### Changed

* Updated dependencies of this package.


## [0.2.2] - 2019-12-16

**This version requires Unity 2019.3.0f1+**

### Changes

* Updated dependencies of this package.


## [0.2.1] - 2019-12-03

### Changes

* Updated dependencies of this package.


## [0.2.0] - 2019-11-22

**This version requires Unity 2019.3 0b11+**

### Changes

* Updated dependencies for this package.


## [0.1.1] - 2019-08-06

### Changes

* Updated dependencies for this package.


## [0.1.0] - 2019-07-30

### Changes

* Updated dependencies for this package.


## [0.0.7-preview.13] - 2019-05-24

### Changes

* Updated dependency for `com.unity.collections`


## [0.0.7-preview.12] - 2019-05-16

### New Features

* IJobParallelForDeferred has been added to allow a parallel for job to be scheduled even if it's for each count will only be known during another jobs execution.

### Upgrade guide
* Previously IJobParallelFor had a overload with the same IJobParallelForDeferred functionality. This is no longer supported since it was not working in Standalone builds using Burst. Now you need to explicitly implement IJobParallelForDeferred if you want to use the deferred schedule parallel for.


## [0.0.7-preview.11] - 2019-05-01

Change tracking started with this version.
