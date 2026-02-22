using System;
using System.Collections.Generic;
using System.Numerics;
using FallDetection.Analytics.Models;

namespace FallDetection.Analytics.Services
{
    public class PoseEstimationService
    {
        private readonly Random _random = new Random();

        // ------------------------------------------------------------
        // Step 3: Analytics computes Encrypted Intermediate Comparison Result (EICR)
        // ------------------------------------------------------------
        public EncryptedIntermediateResults ComputeIntermediateResults(EncryptedPoseFeatures req)
        {
            var t30 = PrivCompAn(req.Tra, 3000);
            var t40 = PrivCompAn(req.Tha, 4000);
            var t80 = PrivCompAn(req.Tra, 8000);
            var t60 = PrivCompAn(req.Tha, 6000);
            
            var tc = PrivComp1An(req.Thl[0], req.Thl[1], req.Cl[0], req.Cl[1], 10, 7);
            var tl = PrivComp1An(req.Trl[0], req.Trl[1], req.Ll[0], req.Ll[1], 10, 5);

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
            long r1 = NextLong(1, (1L << 22) - 1);
            long r2 = NextLong(1, (1L << 10) - 1);

            BigInteger cth0 = BigInteger.Parse(cthStr[0]);
            BigInteger cth1 = BigInteger.Parse(cthStr[1]);

            BigInteger c111 = r2 + (r1 * 2 * (cth0 - cs));
            BigInteger c121 = r2 + (r1 * 2 * (cth1 - cs));

            return new List<string> { c111.ToString(), c121.ToString() };
        }

        private List<string> PrivComp1An(string cth11Str, string cth21Str, string cth3Str, string cth4Str, long factor1, long factor2)
        {
            long r1 = NextLong(1, (1L << 22) - 1);
            long r2 = NextLong(1, (1L << 10) - 1);

            BigInteger cth11 = BigInteger.Parse(cth11Str) * factor1;
            BigInteger cth21 = BigInteger.Parse(cth21Str) * factor1;
            BigInteger cth3 = BigInteger.Parse(cth3Str) * factor2;
            BigInteger cth4 = BigInteger.Parse(cth4Str) * factor2;

            BigInteger c11 = r2 + (r1 * 2 * (cth11 - cth3));
            BigInteger c12 = r2 + (r1 * 2 * (cth21 - cth4));

            return new List<string> { c11.ToString(), c12.ToString() };
        }

        private long NextLong(long min, long max)
        {
            long range = max - min;
            if (range <= 0) return min;
            byte[] buffer = new byte[8];
            _random.NextBytes(buffer);
            long rand = BitConverter.ToInt64(buffer, 0);
            return min + Math.Abs(rand % range);
        }

        // ------------------------------------------------------------
        // Step 5: Polynomial evaluation (MSB and LSB)
        // ------------------------------------------------------------
        public EvaluationResult EvaluatePolynomial(EncryptedComparisonResults comp)
        {
            var pr = new List<string>();
            
            for (int i = 0; i < 6; i++)
            {
                // Parse BigInteger from the strings provided by Caregiver
                BigInteger c1 = BigInteger.Parse(comp.CompA[i]);
                BigInteger c2 = BigInteger.Parse(comp.CompB[i]);
                BigInteger c3 = BigInteger.Parse(comp.CompC[i]);
                BigInteger c4 = BigInteger.Parse(comp.CompD[i]);
                BigInteger c5 = BigInteger.Parse(comp.CompE[i]);
                BigInteger c6 = BigInteger.Parse(comp.CompF[i]);

                // LSB
                BigInteger prl = (c1 * c2 * c4) + (c1 * (1 - c2)) + (1 - c3) + ((1 - c1) * c3 * (1 - c6));
                
                // MSB
                BigInteger prm = (c1 * c2 * (1 - c4) * c5) + ((1 - c1) * c3 * c6) + (1 - c3) + ((1 - c1) * c3 * (1 - c6));
                
                // Result
                pr.Add((prm * 2 + prl).ToString());
            }

            return new EvaluationResult { PolynomialResults = pr };
        }
    }
}

