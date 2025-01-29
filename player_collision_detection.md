## Overview
This subplan implements the logic to detect if the player's collider is hitting a wall from the left or right.

## Problem Description
The player's movement needs to be checked against wall collisions to ensure proper speed adjustments and prevent moving through walls.

## Goals
1. Implement a method to detect collisions with walls.
2. Determine the direction of collision (left or right).
3. Adjust the player's speed based on the direction of collision.

## Additional Notes and Constraints
- The collision detection should be performed in the `Update` method.
- The speed adjustment should be applied only when the player is moving towards the wall.

## References
- [Unity Rigidbody2D Documentation](https://docs.unity3d.com/ScriptReference/Rigidbody2D.html)
- [Unity Collision Detection Documentation](https://docs.unity3d.com/Manual/CollidersOverview.html)
