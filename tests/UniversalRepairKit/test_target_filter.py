"""Regression contract for the native universal repair kit ItemsFilter.

It deliberately reuses the proven single category-matcher schema of the
vanilla weapon repair kit. A previous version chained a required subcategory
102 and several component filters, which excluded every normal head.
"""
import importlib.util
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tools" / "generate_universal_repair_catalog.py"
spec = importlib.util.spec_from_file_location("kit_catalog", SOURCE)
assert spec is not None and spec.loader is not None
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)


class UniversalFilterContract(unittest.TestCase):
    def test_exact_native_filter_shape(self):
        target = kit.build_repair_target_filter()
        self.assertEqual(target["$t"], 43446)
        self.assertEqual(target["_filterPolicy"], 0)
        self.assertEqual(len(target["_filtersList"]), 1)
        child = target["_filtersList"][0]
        self.assertEqual(child["$t"], 39169)
        self.assertEqual(child["_filterPolicy"], 1)
        self.assertEqual(set(child), {"$t", "_filterPolicy", "_itemCategoryIds"})
        self.assertNotIn("_componentTypeNames", str(child))
        self.assertNotIn("_itemSubCategoryIds", str(child))

    def test_equipment_examples_from_current_game_catalog(self):
        categories = set(kit.build_repair_target_filter()["_filtersList"][0]["_itemCategoryIds"])
        cases = {
            "Head_01_Model_01 (13/32)": 13,
            "Armored Hand Left 01": 9,
            "HB 4 helmet (7/31)": 7,
            "Tactical assault backpack": 5,
            "1A4M firearm": 1,
            "Shocker melee weapon": 11,
            "Bodypart modification": 31,
            "Ballistic Shield_01 (24/102)": 24,
        }
        for name, category in cases.items():
            with self.subTest(name=name):
                self.assertIn(category, categories)

    def test_no_consumable_repair_loops_or_key_repairs(self):
        categories = set(kit.build_repair_target_filter()["_filtersList"][0]["_itemCategoryIds"])
        for category in (14, 23, 17, 29):
            with self.subTest(category=category):
                self.assertNotIn(category, categories)

    def test_filter_is_deterministic(self):
        self.assertEqual(kit.build_repair_target_filter(), kit.build_repair_target_filter())


if __name__ == "__main__":
    unittest.main()
