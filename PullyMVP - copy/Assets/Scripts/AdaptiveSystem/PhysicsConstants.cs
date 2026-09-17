using UnityEngine;

/// <summary>
/// Global constants for the conceptual aid system.
///
/// The force-arrow scale MUST be global and identical across every question and
/// every object. Never scale an arrow to make it fit the layout: once an arrow
/// is scaled, its length is no longer comparable to any other arrow and the
/// whole visual argument collapses.
///
/// Reference: one 25 g weight -> 4 cm of arrow.
/// </summary>
public static class PhysicsConstants
{
    public const float G = 9.81f;

    /// <summary>Mass of one standard weight unit (25 g), in kilograms.</summary>
    public const float UnitWeightMass = 0.025f;

    /// <summary>
    /// Arrow length that represents the weight of one unit, in metres.
    /// Not a const: ConceptualAidManager pushes its Inspector value in here at
    /// startup so the scale can be tuned in one place. It stays global - every
    /// arrow in every question reads the same number.
    /// </summary>
    public static float UnitArrowLength = 0.025f;

    /// <summary>
    /// Metres of arrow per Newton of force. 0.04 / (0.025 * 9.81) = 0.16310...
    /// </summary>
    public static float MetersPerNewton
    {
        get { return UnitArrowLength / (UnitWeightMass * G); }
    }

    /// <summary>Convert a force in Newtons to an arrow length in metres.</summary>
    public static float ForceToLength(float newtons)
    {
        return Mathf.Abs(newtons) * MetersPerNewton;
    }

    /// <summary>Convert a mass in kilograms to the arrow length of its weight.</summary>
    public static float MassToLength(float kilograms)
    {
        return ForceToLength(Mathf.Abs(kilograms) * G);
    }

    // ── Aid colours (fixed by the design spec) ────────────────────────────

    /// <summary>Tension arrows: coral #D85A30</summary>
    public static readonly Color TensionColor = new Color(0.847f, 0.353f, 0.188f, 1f);

    /// <summary>Weight arrows: purple #6A4C93</summary>
    public static readonly Color WeightColor = new Color(0.416f, 0.298f, 0.576f, 1f);

    /// <summary>Displacement tracks: blue #185FA5. Reserved for displacement only.</summary>
    public static readonly Color TrackColor = new Color(0.094f, 0.373f, 0.647f, 1f);
}