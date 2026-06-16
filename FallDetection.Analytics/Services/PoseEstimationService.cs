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
                req.Tra?.Length ?? 0,
                req.Tha?.Length ?? 0,
                req.Thl?.Length ?? 0,
                req.Cl?.Length ?? 0,
                req.Trl?.Length ?? 0,
                req.Ll?.Length ?? 0
            );

            if (req.Tra == null || req.Tha == null || req.Thl == null || req.Cl == null || req.Trl == null || req.Ll == null)
            {
                _logger.LogWarning("HME Step 3 aborted: one or more encrypted feature lists are null.");
                throw new ArgumentException("Encrypted pose feature lists must not be null.", nameof(req));
            }

            if (req.Tra.Length != 2 || req.Tha.Length != 2 || req.Thl.Length != 2 || req.Cl.Length != 2 || req.Trl.Length != 2 || req.Ll.Length != 2)
            {
                _logger.LogWarning("HME Step 3 aborted: one or more encrypted feature lists do not contain exactly 2 values.");
                throw new ArgumentException("Each encrypted pose feature list must contain exactly 2 values.", nameof(req));
            }

            // Parse encrypted values as BigInteger
            BigInteger tra1 = BigInteger.Parse(req.Tra[0]);
            BigInteger tra2 = BigInteger.Parse(req.Tra[1]);
            BigInteger tha1 = BigInteger.Parse(req.Tha[0]);
            BigInteger tha2 = BigInteger.Parse(req.Tha[1]);
            BigInteger thl1 = BigInteger.Parse(req.Thl[0]);
            BigInteger thl2 = BigInteger.Parse(req.Thl[1]);
            BigInteger cl1 = BigInteger.Parse(req.Cl[0]);
            BigInteger cl2 = BigInteger.Parse(req.Cl[1]);
            BigInteger trl1 = BigInteger.Parse(req.Trl[0]);
            BigInteger trl2 = BigInteger.Parse(req.Trl[1]);
            BigInteger ll1 = BigInteger.Parse(req.Ll[0]);
            BigInteger ll2 = BigInteger.Parse(req.Ll[1]);

            var t30 = PrivCompAn(tra1, tra2, 3000);
            _logger.LogInformation("HME EICR metric T30 complete.");
            
            var t40 = PrivCompAn(tha1, tha2, 4000);
            _logger.LogInformation("HME EICR metric T40 complete.");
            
            var t80 = PrivCompAn(tra1, tra2, 8000);
            _logger.LogInformation("HME EICR metric T80 complete.");
            
            var t60 = PrivCompAn(tha1, tha2, 6000);
            _logger.LogInformation("HME EICR metric T60 complete.");
            
            // TC: thigh < calf? (Thl * 10 vs cl * 7)
            var tc = PrivComp1An(thl1 * 10, thl2 * 10, cl1 * 7, cl2 * 7);
            _logger.LogInformation("HME EICR metric TC complete.");
            
            // TL: torso < leg? (Trl * 10 vs ll * 5)
            var tl = PrivComp1An(trl1 * 10, trl2 * 10, ll1 * 5, ll2 * 5);
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

        /// <summary>
        /// Server-side comparison with plaintext threshold
        /// Used for: Comparing encrypted value with threshold
        /// Matches Python _priv_comp_an(cth1, cth2, cs)
        /// </summary>
        private List<string> PrivCompAn(BigInteger cth1, BigInteger cth2, long cs)
        {
            _logger.LogInformation("HME PrivCompAn started with threshold {Threshold}.", cs);
            long r1 = NextLong(1, (1L << 22) - 1);
            long r2 = NextLong(1, (1L << 10) - 1);
            _logger.LogDebug("HME PrivCompAn randomness generated: r1={R1}, r2={R2}.", r1, r2);

            // c111 = r2 + (r1 * 2 * (cth1 - cs))
            // c121 = r2 + (r1 * 2 * (cth2 - cs))
            BigInteger c111 = r2 + (r1 * 2 * (cth1 - cs));
            BigInteger c121 = r2 + (r1 * 2 * (cth2 - cs));
            _logger.LogDebug(
                "HME PrivCompAn output values computed: c111={C111}, c121={C121}.",
                FormatBigInteger(c111),
                FormatBigInteger(c121)
            );
            _logger.LogInformation("HME PrivCompAn finished for threshold {Threshold}.", cs);

            return new List<string> { c111.ToString(), c121.ToString() };
        }

        /// <summary>
        /// Server-side encrypted vs encrypted comparison
        /// Used for: Comparing two encrypted values
        /// Matches Python _priv_comp1_an(cth11, cth21, cth3, cth4)
        /// </summary>
        private List<string> PrivComp1An(BigInteger cth11, BigInteger cth21, BigInteger cth3, BigInteger cth4)
        {
            _logger.LogInformation("HME PrivComp1An started.");
            long r1 = NextLong(1, (1L << 22) - 1);
            long r2 = NextLong(1, (1L << 10) - 1);
            _logger.LogDebug("HME PrivComp1An randomness generated: r1={R1}, r2={R2}.", r1, r2);

            // c11 = r2 + (r1 * 2 * (cth11 - cth3))
            // c12 = r2 + (r1 * 2 * (cth21 - cth4))
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
                comp.CompA?.Length ?? 0,
                comp.CompB?.Length ?? 0,
                comp.CompC?.Length ?? 0,
                comp.CompD?.Length ?? 0,
                comp.CompE?.Length ?? 0,
                comp.CompF?.Length ?? 0
            );

            if (comp.CompA == null || comp.CompB == null || comp.CompC == null || comp.CompD == null || comp.CompE == null || comp.CompF == null)
            {
                _logger.LogWarning("HME Step 5 aborted: one or more polynomial comparison arrays are null.");
                throw new ArgumentException("Polynomial comparison arrays must not be null.", nameof(comp));
            }

            if (comp.CompA.Length != 6 || comp.CompB.Length != 6 || comp.CompC.Length != 6 || comp.CompD.Length != 6 || comp.CompE.Length != 6 || comp.CompF.Length != 6)
            {
                _logger.LogWarning("HME Step 5 aborted: one or more polynomial comparison arrays do not contain exactly 6 values.");
                throw new ArgumentException("Each polynomial comparison array must contain exactly 6 values.", nameof(comp));
            }
            
            for (int i = 0; i < 6; i++)
            {
                _logger.LogInformation("HME polynomial iteration {Iteration} started.", i);
                // Parse BigInteger from the strings provided by Caregiver
                // a = T30, b = T40, c = T80, d = TC, e = TL, f = T60
                BigInteger c1 = BigInteger.Parse(comp.CompA[i]); // a = T30
                BigInteger c2 = BigInteger.Parse(comp.CompB[i]); // b = T40
                BigInteger c3 = BigInteger.Parse(comp.CompC[i]); // c = T80
                BigInteger c4 = BigInteger.Parse(comp.CompD[i]); // d = TC
                BigInteger c5 = BigInteger.Parse(comp.CompE[i]); // e = TL
                BigInteger c6 = BigInteger.Parse(comp.CompF[i]); // f = T60
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

                // LSB = (a*b*d) + (a*(1-b)) + (1-c) + ((1-a)*c*(1-f))
                BigInteger prl = (c1 * c2 * c4) + (c1 * (1 - c2)) + (1 - c3) + ((1 - c1) * c3 * (1 - c6));
                
                // MSB = (a*b*(1-d)*e) + ((1-a)*c*f) + (1-c) + ((1-a)*c*(1-f))
                BigInteger prm = (c1 * c2 * (1 - c4) * c5) + ((1 - c1) * c3 * c6) + (1 - c3) + ((1 - c1) * c3 * (1 - c6));
                
                // Result = MSB*2 + LSB
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

            return new EvaluationResult { PolynomialResults = pr.ToArray() };
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