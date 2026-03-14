using System;
using System.Collections.Generic;
using System.Numerics;
using FallDetection.Analytics.Models;
using Microsoft.Extensions.Logging;

namespace FallDetection.Analytics.Services
{
    public class PoseEstimationService
    {
        private readonly Random _random = new Random();
        private readonly ILogger<PoseEstimationService> _logger;

        public PoseEstimationService(ILogger<PoseEstimationService> logger)
        {
            _logger = logger;
        }

        // ------------------------------------------------------------
        // Step 3: Analytics computes Encrypted Intermediate Comparison Result (EICR)
        // ------------------------------------------------------------
        public EncryptedIntermediateResults ComputeIntermediateResults(EncryptedPoseFeatures req)
        {
            _logger.LogInformation("HME Step 3 started: computing encrypted intermediate comparison results (EICR).");
            _logger.LogInformation(
                "HME input summary - TRA count: {TraCount}, THA count: {ThaCount}, THL count: {ThlCount}, CL count: {ClCount}, TRL count: {TrlCount}, LL count: {LlCount}.",
                req.Tra?.Count ?? 0,
                req.Tha?.Count ?? 0,
                req.Thl?.Count ?? 0,
                req.Cl?.Count ?? 0,
                req.Trl?.Count ?? 0,
                req.Ll?.Count ?? 0
            );

            if (req.Tra == null || req.Tha == null || req.Thl == null || req.Cl == null || req.Trl == null || req.Ll == null)
            {
                _logger.LogWarning("HME Step 3 aborted: one or more encrypted feature lists are null.");
                throw new ArgumentException("Encrypted pose feature lists must not be null.", nameof(req));
            }

            if (req.Tra.Count < 2 || req.Tha.Count < 2 || req.Thl.Count < 2 || req.Cl.Count < 2 || req.Trl.Count < 2 || req.Ll.Count < 2)
            {
                _logger.LogWarning("HME Step 3 aborted: one or more encrypted feature lists do not contain the minimum required 2 values.");
                throw new ArgumentException("Each encrypted pose feature list must contain at least 2 values.", nameof(req));
            }

            var t30 = PrivCompAn(req.Tra, 3000);
            _logger.LogInformation("HME EICR metric T30 complete.");
            var t40 = PrivCompAn(req.Tha, 4000);
            _logger.LogInformation("HME EICR metric T40 complete.");
            var t80 = PrivCompAn(req.Tra, 8000);
            _logger.LogInformation("HME EICR metric T80 complete.");
            var t60 = PrivCompAn(req.Tha, 6000);
            _logger.LogInformation("HME EICR metric T60 complete.");
            
            var tc = PrivComp1An(req.Thl[0], req.Thl[1], req.Cl[0], req.Cl[1], 10, 7);
            _logger.LogInformation("HME EICR metric TC complete.");
            var tl = PrivComp1An(req.Trl[0], req.Trl[1], req.Ll[0], req.Ll[1], 10, 5);
            _logger.LogInformation("HME EICR metric TL complete.");

            _logger.LogInformation("HME Step 3 finished: all EICR metrics generated.");

            return new EncryptedIntermediateResults
            {
                T30 = t30,
                T40 = t40,
                T80 = t80,
                T60 = t60,
                TC = tc,
                TL = tl
            };
        }

        private List<string> PrivCompAn(List<string> cthStr, long cs)
        {
            _logger.LogInformation("HME PrivCompAn started with threshold {Threshold}.", cs);
            long r1 = NextLong(1, (1L << 22) - 1);
            long r2 = NextLong(1, (1L << 10) - 1);
            _logger.LogDebug("HME PrivCompAn randomness generated: r1={R1}, r2={R2}.", r1, r2);

            BigInteger cth0 = BigInteger.Parse(cthStr[0]);
            BigInteger cth1 = BigInteger.Parse(cthStr[1]);
            _logger.LogDebug(
                "HME PrivCompAn parsed encrypted features: cth0={Cth0}, cth1={Cth1}.",
                FormatBigInteger(cth0),
                FormatBigInteger(cth1)
            );

            BigInteger c111 = r2 + (r1 * 2 * (cth0 - cs));
            BigInteger c121 = r2 + (r1 * 2 * (cth1 - cs));
            _logger.LogDebug(
                "HME PrivCompAn output values computed: c111={C111}, c121={C121}.",
                FormatBigInteger(c111),
                FormatBigInteger(c121)
            );
            _logger.LogInformation("HME PrivCompAn finished for threshold {Threshold}.", cs);

            return new List<string> { c111.ToString(), c121.ToString() };
        }

        private List<string> PrivComp1An(string cth11Str, string cth21Str, string cth3Str, string cth4Str, long factor1, long factor2)
        {
            _logger.LogInformation("HME PrivComp1An started with factors factor1={Factor1}, factor2={Factor2}.", factor1, factor2);
            long r1 = NextLong(1, (1L << 22) - 1);
            long r2 = NextLong(1, (1L << 10) - 1);
            _logger.LogDebug("HME PrivComp1An randomness generated: r1={R1}, r2={R2}.", r1, r2);

            BigInteger cth11 = BigInteger.Parse(cth11Str) * factor1;
            BigInteger cth21 = BigInteger.Parse(cth21Str) * factor1;
            BigInteger cth3 = BigInteger.Parse(cth3Str) * factor2;
            BigInteger cth4 = BigInteger.Parse(cth4Str) * factor2;
            _logger.LogDebug(
                "HME PrivComp1An scaled values: cth11={Cth11}, cth21={Cth21}, cth3={Cth3}, cth4={Cth4}.",
                FormatBigInteger(cth11),
                FormatBigInteger(cth21),
                FormatBigInteger(cth3),
                FormatBigInteger(cth4)
            );

            BigInteger c11 = r2 + (r1 * 2 * (cth11 - cth3));
            BigInteger c12 = r2 + (r1 * 2 * (cth21 - cth4));
            _logger.LogDebug(
                "HME PrivComp1An output values computed: c11={C11}, c12={C12}.",
                FormatBigInteger(c11),
                FormatBigInteger(c12)
            );
            _logger.LogInformation("HME PrivComp1An finished.");

            return new List<string> { c11.ToString(), c12.ToString() };
        }

        private long NextLong(long min, long max)
        {
            if (max < min) return min;

            // Python randint(a, b) is inclusive on both ends.
            // Match that behavior with .NET Random.NextInt64(min, maxExclusive)
            // by passing (max + 1) as the exclusive upper bound.
            long exclusiveMax = (max == long.MaxValue) ? long.MaxValue : max + 1;
            long result = _random.NextInt64(min, exclusiveMax);
            _logger.LogTrace("HME random generated in range [{Min}, {Max}] inclusive: {Result}.", min, max, result);
            return result;
        }

        // ------------------------------------------------------------
        // Step 5: Polynomial evaluation (MSB and LSB)
        // ------------------------------------------------------------
        public EvaluationResult EvaluatePolynomial(EncryptedComparisonResults comp)
        {
            _logger.LogInformation("HME Step 5 started: polynomial evaluation (MSB/LSB).");
            var pr = new List<string>();
            _logger.LogInformation(
                "HME polynomial input summary - A:{A}, B:{B}, C:{C}, D:{D}, E:{E}, F:{F}.",
                comp.CompA?.Count ?? 0,
                comp.CompB?.Count ?? 0,
                comp.CompC?.Count ?? 0,
                comp.CompD?.Count ?? 0,
                comp.CompE?.Count ?? 0,
                comp.CompF?.Count ?? 0
            );

            if (comp.CompA == null || comp.CompB == null || comp.CompC == null || comp.CompD == null || comp.CompE == null || comp.CompF == null)
            {
                _logger.LogWarning("HME Step 5 aborted: one or more polynomial comparison arrays are null.");
                throw new ArgumentException("Polynomial comparison arrays must not be null.", nameof(comp));
            }

            if (comp.CompA.Count < 6 || comp.CompB.Count < 6 || comp.CompC.Count < 6 || comp.CompD.Count < 6 || comp.CompE.Count < 6 || comp.CompF.Count < 6)
            {
                _logger.LogWarning("HME Step 5 aborted: one or more polynomial comparison arrays do not contain the required 6 values.");
                throw new ArgumentException("Each polynomial comparison array must contain at least 6 values.", nameof(comp));
            }
            
            for (int i = 0; i < 6; i++)
            {
                _logger.LogInformation("HME polynomial iteration {Iteration} started.", i);
                // Parse BigInteger from the strings provided by Caregiver
                BigInteger c1 = BigInteger.Parse(comp.CompA[i]);
                BigInteger c2 = BigInteger.Parse(comp.CompB[i]);
                BigInteger c3 = BigInteger.Parse(comp.CompC[i]);
                BigInteger c4 = BigInteger.Parse(comp.CompD[i]);
                BigInteger c5 = BigInteger.Parse(comp.CompE[i]);
                BigInteger c6 = BigInteger.Parse(comp.CompF[i]);
                _logger.LogDebug(
                    "HME polynomial iteration {Iteration} inputs parsed: c1={C1}, c2={C2}, c3={C3}, c4={C4}, c5={C5}, c6={C6}.",
                    i,
                    FormatBigInteger(c1),
                    FormatBigInteger(c2),
                    FormatBigInteger(c3),
                    FormatBigInteger(c4),
                    FormatBigInteger(c5),
                    FormatBigInteger(c6)
                );

                // LSB
                BigInteger prl = (c1 * c2 * c4) + (c1 * (1 - c2)) + (1 - c3) + ((1 - c1) * c3 * (1 - c6));
                
                // MSB
                BigInteger prm = (c1 * c2 * (1 - c4) * c5) + ((1 - c1) * c3 * c6) + (1 - c3) + ((1 - c1) * c3 * (1 - c6));
                
                // Result
                BigInteger result = (prm * 2 + prl);
                pr.Add(result.ToString());
                _logger.LogInformation(
                    "HME polynomial iteration {Iteration} complete: prl={Prl}, prm={Prm}, result={Result}.",
                    i,
                    FormatBigInteger(prl),
                    FormatBigInteger(prm),
                    FormatBigInteger(result)
                );
            }

            _logger.LogInformation("HME Step 5 finished: {ResultCount} polynomial outputs generated.", pr.Count);

            return new EvaluationResult { PolynomialResults = pr };
        }

        private static string FormatBigInteger(BigInteger value)
        {
            string text = value.ToString();
            return text.Length <= 24
                ? text
                : $"{text.Substring(0, 10)}...{text.Substring(text.Length - 10)} (len={text.Length})";
        }
    }
}

