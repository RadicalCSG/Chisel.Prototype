# Chisel.Prototype

This is a *work in progress* prototype for the Chisel level design editor.
It is unfinished, and has bugs.

Chisel requires Unity 2020.1 or newer

Notes:
- CSG algorithm is still under development

Known issues

* There are some cases where triangulation fails (please collect failure cases so we can later verify we fixed them)
* Moving a brush using the inspector will not always update it's touching brushes
* Intersection vertices aren't yet merged between brushes, which can cause tiny gaps between polygons

Packages overview:
* `com.chisel.core` Low-level API and functionality
* `com.chisel.editor` Unity Scene Editor functionality and API, tools and UI
* `com.chisel.components` Unity Monobehaviour runtime API (to allow for possible ECS replacement in future)
* `com.scene.handles.extensions` Custom scene handle extensions


The official Discord server can be found here: https://discord.gg/zttNkPQ
