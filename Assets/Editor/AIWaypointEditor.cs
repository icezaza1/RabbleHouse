using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIWaypoint))]
public class AIWaypointEditor : Editor
{
    private static readonly Color OutgoingLinkColor = new Color(0.2f, 1f, 0.35f, 1f);
    private static readonly Color MissingReverseLinkColor = new Color(1f, 0.55f, 0.1f, 1f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AIWaypoint waypoint = (AIWaypoint)target;
        List<string> issues = GetIssues(waypoint);
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("Neighbor links look valid and reciprocal.", MessageType.Info);
            return;
        }

        for (int i = 0; i < issues.Count; i++)
            EditorGUILayout.HelpBox(issues[i], MessageType.Warning);
    }

    private void OnSceneGUI()
    {
        AIWaypoint waypoint = (AIWaypoint)target;
        if (waypoint == null)
            return;

        Vector3 start = waypoint.transform.position;
        float handleSize = HandleUtility.GetHandleSize(start);
        DrawWaypointMarker(start, handleSize, Color.cyan, waypoint.name);

        AIWaypoint[] neighbors = waypoint.neighbors;
        if (neighbors == null)
            return;

        for (int i = 0; i < neighbors.Length; i++)
        {
            AIWaypoint neighbor = neighbors[i];
            if (neighbor == null)
                continue;

            Vector3 end = neighbor.transform.position;
            bool reciprocal = HasNeighbor(neighbor, waypoint);
            Handles.color = reciprocal ? OutgoingLinkColor : MissingReverseLinkColor;
            Handles.DrawAAPolyLine(3f, start, end);
            DrawArrow(start, end, handleSize * 0.12f);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = Handles.color;
            string directionLabel = reciprocal ? "↔" : "→ one-way";
            Handles.Label(Vector3.Lerp(start, end, 0.5f) + Vector3.up * handleSize * 0.08f,
                neighbor.name + " " + directionLabel, labelStyle);

            DrawWaypointMarker(end, HandleUtility.GetHandleSize(end), Handles.color, null);
        }

        Handles.color = Color.white;
    }

    private static List<string> GetIssues(AIWaypoint waypoint)
    {
        List<string> issues = new List<string>();
        if (waypoint.neighbors == null)
        {
            issues.Add("Neighbor array is null.");
            return issues;
        }

        HashSet<AIWaypoint> seen = new HashSet<AIWaypoint>();
        for (int i = 0; i < waypoint.neighbors.Length; i++)
        {
            AIWaypoint neighbor = waypoint.neighbors[i];
            if (neighbor == null)
            {
                issues.Add("Neighbor slot " + i + " is empty or missing.");
                continue;
            }

            if (neighbor == waypoint)
                issues.Add("Waypoint links to itself.");
            else if (!seen.Add(neighbor))
                issues.Add("Waypoint " + neighbor.name + " is listed more than once.");
            else if (!HasNeighbor(neighbor, waypoint))
                issues.Add("Link to " + neighbor.name + " is one-way; add this waypoint to its neighbors if travel should work both ways.");
        }

        return issues;
    }

    private static bool HasNeighbor(AIWaypoint waypoint, AIWaypoint candidate)
    {
        if (waypoint == null || waypoint.neighbors == null)
            return false;

        for (int i = 0; i < waypoint.neighbors.Length; i++)
            if (waypoint.neighbors[i] == candidate)
                return true;

        return false;
    }

    private static void DrawWaypointMarker(Vector3 position, float size, Color color, string label)
    {
        Handles.color = color;
        Handles.SphereHandleCap(0, position, Quaternion.identity, size * 0.12f, EventType.Repaint);
        if (!string.IsNullOrEmpty(label))
            Handles.Label(position + Vector3.up * size * 0.18f, label, EditorStyles.boldLabel);
    }

    private static void DrawArrow(Vector3 start, Vector3 end, float size)
    {
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 arrowPosition = Vector3.Lerp(start, end, 0.65f);
        Quaternion arrowRotation = Quaternion.LookRotation(direction.normalized);
        Handles.ConeHandleCap(0, arrowPosition, arrowRotation, size, EventType.Repaint);
    }
}
