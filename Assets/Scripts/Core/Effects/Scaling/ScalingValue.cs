using System;
using UnityEngine;

[Serializable]
public struct ScalingValue
{
    [SerializeField]
    private float fixedAmount;
    [SerializeField]
    private float sourceAttackPowerScale;
    [SerializeField]
    private float sourceResourceScale;
    [SerializeField]
    private float targetCurrentHealthScale;
    [SerializeField]
    private float targetMaximumHealthScale;
    [SerializeField]
    private float sourceStatusStacksScale;
    [SerializeField]
    private float targetStatusStacksScale;

    public float FixedAmount => fixedAmount;
    public float SourceAttackPowerScale => sourceAttackPowerScale;
    public float SourceResourceScale => sourceResourceScale;
    public float TargetCurrentHealthScale => targetCurrentHealthScale;
    public float TargetMaximumHealthScale => targetMaximumHealthScale;
    public float SourceStatusStacksScale => sourceStatusStacksScale;
    public float TargetStatusStacksScale => targetStatusStacksScale;
    public bool IsFinite =>
        IsFiniteValue(fixedAmount) &&
        IsFiniteValue(sourceAttackPowerScale) &&
        IsFiniteValue(sourceResourceScale) &&
        IsFiniteValue(targetCurrentHealthScale) &&
        IsFiniteValue(targetMaximumHealthScale) &&
        IsFiniteValue(sourceStatusStacksScale) &&
        IsFiniteValue(targetStatusStacksScale);
    public bool HasPositiveTerm =>
        fixedAmount > 0f ||
        sourceAttackPowerScale > 0f ||
        sourceResourceScale > 0f ||
        targetCurrentHealthScale > 0f ||
        targetMaximumHealthScale > 0f ||
        sourceStatusStacksScale > 0f ||
        targetStatusStacksScale > 0f;
    public bool HasNonZeroTerm =>
        fixedAmount != 0f ||
        sourceAttackPowerScale != 0f ||
        sourceResourceScale != 0f ||
        targetCurrentHealthScale != 0f ||
        targetMaximumHealthScale != 0f ||
        sourceStatusStacksScale != 0f ||
        targetStatusStacksScale != 0f;
    public bool HasTargetDependentTerm =>
        targetCurrentHealthScale != 0f ||
        targetMaximumHealthScale != 0f ||
        targetStatusStacksScale != 0f;

    public ScalingValue(
        float fixedAmount,
        float sourceAttackPowerScale)
        : this(
            fixedAmount,
            sourceAttackPowerScale,
            0f)
    {
    }

    public ScalingValue(
        float fixedAmount,
        float sourceAttackPowerScale,
        float sourceResourceScale)
        : this(
            fixedAmount,
            sourceAttackPowerScale,
            sourceResourceScale,
            0f,
            0f,
            0f,
            0f)
    {
    }

    public ScalingValue(
        float fixedAmount,
        float sourceAttackPowerScale,
        float sourceResourceScale,
        float targetCurrentHealthScale,
        float targetMaximumHealthScale,
        float sourceStatusStacksScale,
        float targetStatusStacksScale)
    {
        this.fixedAmount = fixedAmount;
        this.sourceAttackPowerScale = sourceAttackPowerScale;
        this.sourceResourceScale = sourceResourceScale;
        this.targetCurrentHealthScale = targetCurrentHealthScale;
        this.targetMaximumHealthScale = targetMaximumHealthScale;
        this.sourceStatusStacksScale = sourceStatusStacksScale;
        this.targetStatusStacksScale = targetStatusStacksScale;
    }

    public float Evaluate(EffectContext context)
    {
        if (!IsFinite)
            return 0f;

        float result = fixedAmount +
                       context.SourceAttackPower *
                       sourceAttackPowerScale +
                       context.SourceResource *
                       sourceResourceScale +
                       context.TargetCurrentHealth *
                       targetCurrentHealthScale +
                       context.TargetMaximumHealth *
                       targetMaximumHealthScale +
                       context.SourceStatusStacks *
                       sourceStatusStacksScale +
                       context.TargetStatusStacks *
                       targetStatusStacksScale;
        return IsFiniteValue(result)
            ? result
            : 0f;
    }

    public static ScalingValue Fixed(float value)
    {
        return new ScalingValue(value, 0f);
    }

    public static ScalingValue SourceAttackPower(float scale)
    {
        return new ScalingValue(0f, scale);
    }

    public static ScalingValue SourceResource(float scale)
    {
        return new ScalingValue(0f, 0f, scale);
    }

    public static ScalingValue TargetCurrentHealth(float scale)
    {
        return new ScalingValue(
            0f,
            0f,
            0f,
            scale,
            0f,
            0f,
            0f);
    }

    public static ScalingValue TargetMaximumHealth(float scale)
    {
        return new ScalingValue(
            0f,
            0f,
            0f,
            0f,
            scale,
            0f,
            0f);
    }

    public static ScalingValue SourceStatusStacks(float scale)
    {
        return new ScalingValue(
            0f,
            0f,
            0f,
            0f,
            0f,
            scale,
            0f);
    }

    public static ScalingValue TargetStatusStacks(float scale)
    {
        return new ScalingValue(
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            scale);
    }

    public static ScalingValue FromLegacy(
        CharacterDamageAmountMode amountMode,
        float amount)
    {
        return amountMode switch
        {
            CharacterDamageAmountMode.Fixed =>
                Fixed(amount),
            CharacterDamageAmountMode.Ratio =>
                SourceAttackPower(amount),
            _ => new ScalingValue(float.NaN, float.NaN)
        };
    }

    public static ScalingValue operator +(
        ScalingValue left,
        ScalingValue right)
    {
        return new ScalingValue(
            left.fixedAmount + right.fixedAmount,
            left.sourceAttackPowerScale +
            right.sourceAttackPowerScale,
            left.sourceResourceScale +
            right.sourceResourceScale,
            left.targetCurrentHealthScale +
            right.targetCurrentHealthScale,
            left.targetMaximumHealthScale +
            right.targetMaximumHealthScale,
            left.sourceStatusStacksScale +
            right.sourceStatusStacksScale,
            left.targetStatusStacksScale +
            right.targetStatusStacksScale);
    }

    private static bool IsFiniteValue(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
