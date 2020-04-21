# Chisel.Prototype

This is a *work in progress* prototype for the Chisel level design editor.
It is unfinished, and has bugs.

Chisel requires Unity 2020.1 or newer
with the project set to ".net standard 2.0" / scripting runtime to ".net 4.x"

Notes:
- CSG algorithm is still under development

Packages overview:
* `com.chisel.core` Low-level API and functionality
* `com.chisel.editor` Unity Scene Editor functionality and API, tools and UI
* `com.chisel.components` Unity Monobehaviour runtime API (to allow for possible ECS replacement in future)
* `com.scene.handles.extensions` Custom scene handle extensions


The official Discord server can be found here: https://discord.gg/zttNkPQ
