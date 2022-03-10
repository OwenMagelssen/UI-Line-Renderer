# UI-Line-Renderer
 
A simple line renderer for UGUI that multithreads most of its workload using Unity's C# Job System. 
UILineRenderer inherits from Unity's [UI.Graphic Component](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/UI.Graphic.html).
This project is setup with Unity 2021.2.6f1 and URP, but should work with most editor versions that support Job System and Burst with any render pipeline.

Example Use:

```
uiLineRenderer.SetPoints(vector2Array);
```

...or slightly more efficiently:

```
uiLineRenderer.SetPoints(float2Array);
```

Features:  
-Set points from script  
-Rounded end caps  
-Simple segment joins  

Considered Future Features:  
-Demo scene  
-Set points in inspector  
-Mitered segment joins  
-Rounded segment joins  
-New end cap shapes  
-Dashed lines  
-Proper UVs and Shaders made for UILineRenderer  


MIT License.
