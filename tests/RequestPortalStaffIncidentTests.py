import importlib.util
import json
import pathlib
import unittest
from unittest.mock import patch

SOURCE = pathlib.Path(__file__).parents[1] / "scripts/Read-RequestPortal-Staff-Incident.py"
spec = importlib.util.spec_from_file_location("incident", SOURCE)
incident = importlib.util.module_from_spec(spec)
spec.loader.exec_module(incident)


class StaffIncidentTests(unittest.TestCase):
    def test_only_staff_page_events_are_returned(self):
        rows = [json.dumps({"MESSAGE": "Unrelated request", "__REALTIME_TIMESTAMP": "1000000"}),
                json.dumps({"MESSAGE": "NpgsqlException at StaffAdministrationService.HistoryAsync", "__REALTIME_TIMESTAMP": "2000000"})]
        result = list(incident.inspect(rows))
        self.assertEqual(1, len(result))
        self.assertIn("HistoryAsync", result[0]["message"])

    def test_credential_and_selection_values_are_redacted(self):
        message = '/Administration/Staff password="fixture-password" client_secret=fixture-secret&state=fixture-state Authorization: Bearer fixture-token Selection=fixture-selection'
        result = incident.redact(message)
        for value in ("fixture-password", "fixture-secret", "fixture-state", "fixture-token", "fixture-selection"):
            self.assertNotIn(value, result)

    def test_reader_has_no_mutating_operations_or_arbitrary_arguments(self):
        from types import SimpleNamespace
        with patch.object(incident.os, "geteuid", return_value=0), patch.object(incident.sys, "argv", ["incident"]), \
             patch.object(incident.subprocess, "run", return_value=SimpleNamespace(returncode=0, stdout="")) as run:
            incident.main()
        self.assertEqual("/usr/bin/journalctl", run.call_args.args[0][0])
        self.assertIn("--unit=aegis-requestportal", run.call_args.args[0])
        with patch.object(incident.os, "geteuid", return_value=0), patch.object(incident.sys, "argv", ["incident", "unapproved"]):
            with self.assertRaises(RuntimeError):
                incident.main()


if __name__ == "__main__":
    unittest.main()
