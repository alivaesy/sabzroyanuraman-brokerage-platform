# -*- coding: utf-8 -*-
"""Export an ArcMap 10.x MXD to a portable JSON manifest.

Run with the Python interpreter bundled with licensed ArcMap 10.8:
  "C:\\Python27\\ArcGIS10.8\\python.exe" tools\\mxd_to_manifest.py map.mxd

This extracts map-document metadata only. It does not copy source datasets.
Unresolved dataSourcePath values must be repaired in WebGIS.
"""
from __future__ import print_function

import datetime
import json
import os
import sys
import traceback

try:
    import arcpy
except Exception as exc:
    sys.stderr.write("ArcPy is unavailable. Run this script with ArcMap 10.8 Python.\n")
    sys.stderr.write(str(exc) + "\n")
    sys.exit(2)


def safe(obj, attr, default=None):
    try:
        value = getattr(obj, attr)
        if callable(value):
            value = value()
        return value
    except Exception:
        return default


def clean(value):
    if value is None:
        return None
    if isinstance(value, (str, int, float, bool)):
        return value
    try:
        if hasattr(value, "RGB"):
            return list(value.RGB)
        if hasattr(value, "red") and hasattr(value, "green") and hasattr(value, "blue"):
            return [value.red, value.green, value.blue]
        if hasattr(value, "name"):
            return str(value.name)
    except Exception:
        pass
    try:
        return str(value)
    except Exception:
        return None


def source_type(path, layer):
    p = (path or "").lower()
    if p.endswith(".shp"):
        return "shapefile"
    if ".gdb" in p:
        return "filegdb_featureclass"
    if ".sde" in p or "sde:" in p:
        return "sde"
    if safe(layer, "isRasterLayer", False):
        return "raster"
    return "other"


def renderer_info(layer, warnings):
    result = {"type": "unsupported", "field": None, "classes": [], "note": None}
    sym_type = str(safe(layer, "symbologyType", "") or "").upper()
    sym = safe(layer, "symbology")
    mapping = {
        "SIMPLE": "simple",
        "UNIQUE_VALUES": "uniqueValue",
        "GRADUATED_COLORS": "graduatedColor",
        "GRADUATED_SYMBOLS": "graduatedSymbol"
    }
    result["type"] = mapping.get(sym_type, "unsupported")
    if sym is None:
        result["note"] = "ArcPy did not expose a symbology object for this layer."
        warnings.append(result["note"])
        return result

    if result["type"] == "uniqueValue":
        result["field"] = safe(sym, "valueField")
        values = safe(sym, "classValues", []) or []
        labels = safe(sym, "classLabels", []) or []
        symbols = safe(sym, "classSymbols", []) or []
        for i, value in enumerate(values):
            symbol = symbols[i] if i < len(symbols) else None
            result["classes"].append({
                "value": clean(value),
                "label": clean(labels[i] if i < len(labels) else value),
                "color": clean(safe(symbol, "color")) if symbol is not None else None
            })
    elif result["type"] in ("graduatedColor", "graduatedSymbol"):
        result["field"] = safe(sym, "valueField")
        values = safe(sym, "classBreakValues", []) or []
        labels = safe(sym, "classBreakLabels", []) or []
        symbols = safe(sym, "classBreakSymbols", []) or []
        previous = None
        for i, upper in enumerate(values):
            symbol = symbols[i] if i < len(symbols) else None
            result["classes"].append({
                "range": {"min": clean(previous), "max": clean(upper)},
                "label": clean(labels[i] if i < len(labels) else upper),
                "color": clean(safe(symbol, "color")) if symbol is not None else None
            })
            previous = upper
    elif result["type"] == "simple":
        symbol = safe(sym, "symbol")
        result["classes"].append({
            "label": "Default symbol",
            "color": clean(safe(symbol, "color")) if symbol is not None else None
        })
    else:
        result["note"] = "Renderer type %s is not mapped; no symbol values were guessed." % sym_type
        warnings.append(result["note"])
    return result


def labeling_info(layer, warnings):
    enabled = bool(safe(layer, "showLabels", False))
    result = {"enabled": enabled, "expression": None, "field": None}
    if not enabled:
        return result
    classes = safe(layer, "labelClasses", []) or []
    if not classes:
        warnings.append("Labels are enabled, but ArcPy returned no label classes.")
        return result
    first = classes[0]
    result["expression"] = safe(first, "expression")
    expression = safe(first, "expression")
    match = __import__("re").match(r"^\s*\[([A-Za-z_][A-Za-z0-9_]*)\]\s*$", str(expression or ""))
    result["field"] = match.group(1) if match else None
    if result["expression"] is None:
        warnings.append("Label expression could not be extracted.")
    return result


def layer_object(layer, parent_group):
    warnings = []
    name = safe(layer, "name", "Unnamed layer")
    is_group = bool(safe(layer, "isGroupLayer", False))
    data_path = safe(layer, "dataSource")
    workspace = safe(layer, "workspacePath")
    dataset = safe(layer, "datasetName")
    full_path = data_path or (os.path.join(workspace, dataset) if workspace and dataset else None)
    item = {
        "name": name,
        "groupLayer": parent_group,
        "isGroupLayer": is_group,
        "visible": bool(safe(layer, "visible", True)),
        "dataSourcePath": full_path,
        "dataSourceType": source_type(full_path, layer),
        "geometryType": None,
        "minScale": clean(safe(layer, "minScale")),
        "maxScale": clean(safe(layer, "maxScale")),
        "definitionQuery": clean(safe(layer, "definitionQuery")),
        "renderer": {"type": "unsupported", "field": None, "classes": []},
        "labeling": {"enabled": False, "expression": None, "field": None},
        "extractionWarnings": warnings
    }
    if is_group:
        item["layers"] = []
        return item

    try:
        desc = arcpy.Describe(layer)
        item["geometryType"] = clean(safe(desc, "shapeType"))
    except Exception as exc:
        warnings.append("Geometry type unavailable: %s" % exc)

    try:
        item["renderer"] = renderer_info(layer, warnings)
    except Exception as exc:
        warnings.append("Renderer extraction failed: %s" % exc)
        item["renderer"] = {"type": "unsupported", "field": None, "classes": [], "note": str(exc)}
    try:
        item["labeling"] = labeling_info(layer, warnings)
    except Exception as exc:
        warnings.append("Label extraction failed: %s" % exc)
    if not full_path:
        warnings.append("Layer has no accessible data source path.")
    return item


def build_layer_tree(flat_layers):
    roots = []
    nodes = {}
    for layer in flat_layers:
        long_name = safe(layer, "longName", safe(layer, "name", "Unnamed layer"))
        parts = [p for p in str(long_name).replace("/", "\\").split("\\") if p]
        if not parts:
            parts = [safe(layer, "name", "Unnamed layer")]
        parent_parts = []
        target = roots
        for part in parts[:-1]:
            parent_parts.append(part)
            key = tuple(parent_parts)
            if key not in nodes:
                group = {
                    "name": part, "groupLayer": " / ".join(parent_parts[:-1]) or None,
                    "isGroupLayer": True, "visible": True, "dataSourcePath": None,
                    "dataSourceType": "other", "geometryType": None, "minScale": None,
                    "maxScale": None, "definitionQuery": None,
                    "renderer": {"type": "unsupported", "field": None, "classes": []},
                    "labeling": {"enabled": False, "expression": None, "field": None},
                    "extractionWarnings": [], "layers": []
                }
                nodes[key] = group
                target.append(group)
            target = nodes[key]["layers"]
        name = parts[-1]
        if bool(safe(layer, "isGroupLayer", False)):
            key = tuple(parent_parts + [name])
            if key not in nodes:
                group = {
                    "name": name, "groupLayer": " / ".join(parent_parts) or None,
                    "isGroupLayer": True, "visible": bool(safe(layer, "visible", True)),
                    "dataSourcePath": None, "dataSourceType": "other", "geometryType": None,
                    "minScale": None, "maxScale": None, "definitionQuery": None,
                    "renderer": {"type": "unsupported", "field": None, "classes": []},
                    "labeling": {"enabled": False, "expression": None, "field": None},
                    "extractionWarnings": [], "layers": []
                }
                nodes[key] = group
                target.append(group)
            else:
                nodes[key]["visible"] = bool(safe(layer, "visible", True))
            continue
        item = layer_object(layer, " / ".join(parent_parts) or None)
        item["name"] = name
        target.append(item)
    return roots


def main():
    if len(sys.argv) < 2:
        sys.stderr.write("Usage: mxd_to_manifest.py <input.mxd> [output.manifest.json]\n")
        return 2
    source = os.path.abspath(sys.argv[1])
    if not os.path.isfile(source) or not source.lower().endswith(".mxd"):
        sys.stderr.write("Input must be an existing .mxd file.\n")
        return 2
    output = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.splitext(source)[0] + ".manifest.json"
    try:
        document = arcpy.mapping.MapDocument(source)
        data_frames = []
        for frame in arcpy.mapping.ListDataFrames(document):
            frame_layers = arcpy.mapping.ListLayers(document, "", frame)
            data_frames.append({
                "name": safe(frame, "name", "Layers"),
                "spatialReference": clean(safe(safe(frame, "spatialReference"), "factoryCode")),
                "layers": build_layer_tree(frame_layers)
            })
        if not data_frames:
            raise RuntimeError("The MXD contains no readable data frames.")
        manifest = {
            "schemaVersion": 1,
            "sourceFile": os.path.basename(source),
            "generatedAt": datetime.datetime.utcnow().replace(microsecond=0).isoformat() + "Z",
            "dataFrames": data_frames
        }
        with open(output, "wb") as handle:
            payload = json.dumps(manifest, ensure_ascii=False, indent=2)
            if not isinstance(payload, bytes):
                payload = payload.encode("utf-8")
            handle.write(payload)
        print("Manifest written: %s" % output)
        print("Data frames: %d" % len(data_frames))
        return 0
    except Exception as exc:
        sys.stderr.write("MXD conversion failed: %s\n" % exc)
        traceback.print_exc(file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
