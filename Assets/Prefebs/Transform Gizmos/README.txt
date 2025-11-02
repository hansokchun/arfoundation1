Transform Gizmos

Please checkout the Demo scene. 

Using this package:

1) Drag the prefab "Gizmo" to a scene.
2) On the root gameobject "Gizmo" assign the target object, which should be transformed, to the "Target Object" field.
2) You can already start running the game.
3) In order to see the gizmos press "R" for rotation, "T" for translation and "Z" for scaling. 

Scene view:
If you want the gizmos to be at the correct place also in scene view make sure that the position, rotation and scale of the root "Gizmo" object are the same as of the target object.

Gizmo size:
On the root gameobject "Gizmo" there is a variable called "Gizmo Size". If you change this size, the gizmos will change their size (in runtime).

Transformation speed:
On the child gameobjects "Rotation", "Translation" and "Scaling" there is a variable called "..speed". If you adjust this value the speed/sensitivity of the transformation will be changed.

Adjust behaviour of showing gizmos:
By default only one transformation is visible at once. In the script "GizmoController" you can change this behaviour. There are also callback functions for showing the transforms.