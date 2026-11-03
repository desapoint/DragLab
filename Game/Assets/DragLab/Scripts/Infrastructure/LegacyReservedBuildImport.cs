using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    // The only new serializer boundary for garage reservation migration.
    // Callers prepare this immutable context before entering a query.
    internal static class LegacyReservedBuildImport
    {
        internal static ReservedBuildContext FromLegacyOwner(PlayerProfile owner)
        {
            var results = new System.Collections.Generic.List<ReservedBuildImportResult>();
            foreach (var job in owner.business.jobs)
            {
                if (job == null) throw new ArgumentNullException("job", "FromLegacyOwner requires validated non-null job rows.");
                var car = owner.cars.Find(value => value.instanceId == job.carId);
                if (car == null) throw new ArgumentException("FromLegacyOwner requires a validated job-to-car reference.", "owner");
                results.Add(FromLegacyCore(owner, car, job));
            }
            return ReservedBuildContext.FromImportedResults(results);
        }

        internal static ReservedBuildImportResult FromLegacy(PlayerProfile owner, OwnedCar car, AssignedGarageJob job)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            if (car == null) throw new ArgumentNullException("car");
            if (job == null) throw new ArgumentNullException("job");
            return FromLegacyCore(owner, car, job);
        }

        static ReservedBuildImportResult FromLegacyCore(PlayerProfile owner, OwnedCar car, AssignedGarageJob job)
        {
            string hash = null;
            string hashType = null, hashMessage = null, hashParameter = null;
            ReservedBuildValues current = null;
            string[] labels = null;
            try { hash = Snapshot(owner, car); }
            catch (Exception error) { CaptureError(error, out hashType, out hashMessage, out hashParameter); }

            string captureType = null, captureMessage = null, captureParameter = null;
            try
            {
                current = ReservedBuildValues.Capture(owner, car);
                labels = car.installed.slots.OrderBy(slot => slot.partInstanceId, StringComparer.Ordinal)
                    .Select(slot => slot.slot).ToArray();
            }
            catch (Exception error) { CaptureError(error, out captureType, out captureMessage, out captureParameter); }

            bool matched = hashType == null && String.Equals(hash, job.snapshot, StringComparison.Ordinal);
            string digest = ReservedBuildImportResult.Digest(job.snapshotJson);
            string textType = null, textMessage = null, textParameter = null;
            ReservedBuildValues textBaseline = null;
            ReservedBuildValues expected = null;
            ReservedBuildFallbackKind fallbackKind;
            string fallbackType = null, fallbackMessage = null, fallbackParameter = null;
            bool fallbackMatched = false;

            if (String.IsNullOrEmpty(job.snapshotJson))
            {
                fallbackKind = ReservedBuildFallbackKind.Absent;
            }
            else
            {
                // The legacy exact-text branch precedes JSON decoding. Keep its
                // typed observation independent from the decoded expectation.
                try
                {
                    var contents = Contents(owner, car);
                    string serialized = JsonUtility.ToJson(contents);
                    if (String.Equals(serialized, job.snapshotJson, StringComparison.Ordinal))
                    {
                        textBaseline = ReservedBuildValues.FromSnapshot(contents);
                        fallbackMatched = true;
                    }
                }
                catch (Exception error) { CaptureError(error, out textType, out textMessage, out textParameter); }

                try
                {
                    var decoded = JsonUtility.FromJson<ReservedBuildSnapshot>(job.snapshotJson);
                    expected = ReservedBuildValues.FromSnapshot(decoded);
                    fallbackKind = ReservedBuildFallbackKind.Values;
                    if (!fallbackMatched && textType == null && current != null)
                        fallbackMatched = expected != null && expected.MatchesCurrent(current, ReservedBuildComparisonMode.LegacyJsonFallback);
                }
                catch (Exception error)
                {
                    CaptureError(error, out fallbackType, out fallbackMessage, out fallbackParameter);
                    fallbackKind = ReservedBuildFallbackKind.Error;
                }

                if (textType != null)
                {
                    fallbackType = textType; fallbackMessage = textMessage; fallbackParameter = textParameter;
                    fallbackKind = ReservedBuildFallbackKind.Error;
                }
            }

            ReservedBuildHashBaseline baseline = matched && current != null && captureType == null
                ? new ReservedBuildHashBaseline(current, labels) : null;
            return new ReservedBuildImportResult(job.id, job.carId, job.snapshot, digest, job.snapshotJson == null,
                matched ? ReservedBuildImportKind.HashMatchedAtImport : ReservedBuildImportKind.HashMismatchAtImport,
                textBaseline, expected, baseline, fallbackKind, fallbackMatched,
                hashType, hashMessage, hashParameter, matched ? captureType : null,
                matched ? captureMessage : null, matched ? captureParameter : null,
                fallbackType, fallbackMessage, fallbackParameter);
        }

        // Literal semantic copy of GarageBusiness.Snapshot and Contents. Keep
        // the order, first-match lookup, concatenation, and serializer calls.
        static string Snapshot(PlayerProfile p, OwnedCar c)
        {
            string text = JsonUtility.ToJson(c.fuel) + JsonUtility.ToJson(c.fluids)
                + JsonUtility.ToJson(c.tune) + c.paintHex;
            foreach (var slot in c.installed.slots.OrderBy(item => item.partInstanceId, StringComparer.Ordinal))
                text += slot.slot + JsonUtility.ToJson(p.parts.Find(item => item.instanceId == slot.partInstanceId));
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        static ReservedBuildSnapshot Contents(PlayerProfile p, OwnedCar c)
        {
            return new ReservedBuildSnapshot
            {
                fuel = c.fuel,
                fluids = c.fluids,
                tune = c.tune,
                paint = c.paintHex,
                parts = c.installed.slots.OrderBy(item => item.partInstanceId, StringComparer.Ordinal)
                    .Select(item => p.parts.Find(part => part.instanceId == item.partInstanceId)).ToArray()
            };
        }

        static void CaptureError(Exception error, out string type, out string message, out string parameter)
        {
            type = error.GetType().FullName;
            message = error.Message;
            parameter = (error as ArgumentException)?.ParamName;
        }
    }
}
