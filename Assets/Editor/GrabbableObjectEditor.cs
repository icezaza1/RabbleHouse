using RabbleHouse;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrabbableObject))]
public class GrabbableObjectEditor : Editor
{
    private void OnSceneGUI()
    {
        GrabbableObject grabbable = (GrabbableObject)target;

        DrawGripPoint(
            grabbable.gripPoint,
            "Primary Grip",
            true
        );

        if (grabbable.secondaryGripPoint != null)
        {
            DrawGripPoint(
                grabbable.secondaryGripPoint,
                "Secondary Grip",
                false
            );

            // Draw a line connecting the two grip points.
            Handles.DrawDottedLine(
                grabbable.gripPoint.position,
                grabbable.secondaryGripPoint.position,
                3f
            );
        }
    }

    private void DrawGripPoint(
            Transform grip,
            string label,
            bool primary)
    {
        if (grip == null)
            return;

        Vector3 position = grip.position;

        // ---------------------------------
        // Label
        // ---------------------------------

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);

        labelStyle.normal.textColor =
            primary ? Color.cyan : Color.yellow;

        Handles.Label(
            position + Vector3.up * 0.08f,
            label,
            labelStyle
        );

        // ---------------------------------
        // Position handle
        // ---------------------------------

        EditorGUI.BeginChangeCheck();

        Vector3 newPosition =
            Handles.PositionHandle(
                position,
                grip.rotation
            );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(
                grip,
                "Move Grip Point"
            );

            grip.position = newPosition;

            EditorUtility.SetDirty(grip);
        }

        // ---------------------------------
        // Rotation handle
        // ---------------------------------

        EditorGUI.BeginChangeCheck();

        Quaternion newRotation =
            Handles.RotationHandle(
                grip.rotation,
                position
            );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(
                grip,
                "Rotate Grip Point"
            );

            grip.rotation = newRotation;

            EditorUtility.SetDirty(grip);
        }

        // ---------------------------------
        // Direction axes
        // ---------------------------------

        float axisSize = HandleUtility.GetHandleSize(position) * 0.35f;

        Handles.color = Color.red;
        Handles.DrawLine(
            position,
            position + grip.right * axisSize
        );

        Handles.color = Color.green;
        Handles.DrawLine(
            position,
            position + grip.up * axisSize
        );

        Handles.color = Color.blue;
        Handles.DrawLine(
            position,
            position + grip.forward * axisSize
        );

        Handles.color = Color.white;

        // ---------------------------------
        // Grip sphere
        // ---------------------------------

        float sphereSize =
            HandleUtility.GetHandleSize(position) * 0.08f;

        Handles.SphereHandleCap(
            0,
            position,
            Quaternion.identity,
            sphereSize,
            EventType.Repaint
        );
    }
}
