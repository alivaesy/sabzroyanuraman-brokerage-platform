# -*- coding: utf-8 -*-
"""
ArcMap 10.8 export worker (run with ArcMap's Python 2.7 interpreter).
Exports a map document into a portable package:
  manifest.json
  layers/<index>_<safe-name>.geojson
  source-data/<index>_<safe-name>.shp (+ sidecar files when export fails to GeoJSON)
The manifest intentionally records unsupported/uncertain properties rather than
silently claiming that ArcMap rendering/topology was fully reproduced.
"""
from __future__ import print_function
import arcpy
import os
import sys
import json
import re
import shutil
import zipfile
import traceback
import datetime

def safe_name(value):
    value = re.sub(r'[^A-Za-z0-9_.-]+', '_', value or 'layer')
    return value[:80] or 'layer'

def json_value(value):
    if value is None or isinstance(value, (bool, int, long, float, str, unicode)):
        return value
    try:
        return unicode(value)
    except Exception:
        return repr(value)

def field_info(field):
    return {
        'name': field.name,
        'alias': getattr(field, 'aliasName', field.name),
        'type': field.type,
        'length': getattr(field, 'length', None),
        'precision': getattr(field, 'precision', None),
        'scale': getattr(field, 'scale', None),
        'nullable': getattr(field, 'isNullable', None),
        'required': getattr(field, 'required', None),
        'domain': getattr(field, 'domain', None)
    }

def describe_layer(layer, index, output_dir):
    item = {
        'index': index,
        'name': getattr(layer, 'longName', getattr(layer, 'name', 'Layer')),
        'visible': getattr(layer, 'visible', None),
        'minScale': getattr(layer, 'minScale', None),
        'maxScale': getattr(layer, 'maxScale', None),
        'showLabels': getattr(layer, 'showLabels', None),
        'definitionQuery': getattr(layer, 'definitionQuery', None),
        'transparency': getattr(layer, 'transparency', None),
        'dataSource': None,
        'dataType': None,
        'shapeType': None,
        'spatialReference': None,
        'extent': None,
        'fields': [],
        'featureCount': None,
        'renderer': None,
        'labelClasses': [],
        'joinsOrRelates': 'not reliably introspected by this worker',
        'export': {'status': 'not-started'},
        'warnings': []
    }
    try:
        item['isGroupLayer'] = bool(layer.isGroupLayer)
    except Exception:
        item['isGroupLayer'] = False
    try:
        item['dataSource'] = getattr(layer, 'dataSource', None)
        desc = arcpy.Describe(layer)
        item['dataType'] = getattr(desc, 'dataType', None)
        if not layer.isFeatureLayer:
            item['warnings'].append('Raster, service, annotation, network and other non-feature layers need type-specific export.')
            return item
    except Exception:
        item['warnings'].append('Layer type could not be determined or described.')
        return item

    try:
        desc = arcpy.Describe(layer)
        item['shapeType'] = getattr(desc, 'shapeType', None)
        sr = getattr(desc, 'spatialReference', None)
        if sr:
            item['spatialReference'] = {
                'name': getattr(sr, 'name', None),
                'factoryCode': getattr(sr, 'factoryCode', None),
                'type': getattr(sr, 'type', None),
                'linearUnitName': getattr(sr, 'linearUnitName', None),
                'angularUnitName': getattr(sr, 'angularUnitName', None)
            }
        ext = getattr(desc, 'extent', None)
        if ext:
            item['extent'] = {
                'xmin': ext.XMin, 'ymin': ext.YMin,
                'xmax': ext.XMax, 'ymax': ext.YMax
            }
        item['fields'] = [field_info(f) for f in arcpy.ListFields(layer)]
        item['featureCount'] = int(arcpy.GetCount_management(layer).getOutput(0))
    except Exception as exc:
        item['warnings'].append('Describe/count/field inspection failed: ' + unicode(exc))

    try:
        sym = layer.symbology
        renderer_type = getattr(sym, 'type', None)
        renderer = {'type': renderer_type}
        if renderer_type == 'SIMPLE':
            renderer['symbolType'] = getattr(sym, 'symbol', None).__class__.__name__
        elif renderer_type == 'UNIQUE_VALUES':
            renderer['valueField'] = getattr(sym, 'valueField', None)
            renderer['classValues'] = []
            try:
                renderer['classValues'] = list(sym.classValues)
            except Exception:
                pass
        elif renderer_type == 'GRADUATED_COLORS':
            renderer['classificationField'] = getattr(sym, 'valueField', None)
            renderer['classificationMethod'] = getattr(sym, 'classificationMethod', None)
            renderer['breakCount'] = getattr(sym, 'breakCount', None)
            renderer['classBreaks'] = []
            try:
                for brk in sym.classBreaks:
                    renderer['classBreaks'].append({
                        'label': getattr(brk, 'label', None),
                        'description': getattr(brk, 'description', None),
                        'minimum': getattr(brk, 'classMinValue', None),
                        'maximum': getattr(brk, 'classMaxValue', None)
                    })
            except Exception:
                pass
        elif renderer_type == 'GRADUATED_SYMBOLS':
            renderer['classificationField'] = getattr(sym, 'valueField', None)
            renderer['classBreaks'] = []
            try:
                for brk in sym.classBreaks:
                    renderer['classBreaks'].append({
                        'label': getattr(brk, 'label', None),
                        'minimum': getattr(brk, 'classMinValue', None),
                        'maximum': getattr(brk, 'classMaxValue', None)
                    })
            except Exception:
                pass
        item['renderer'] = renderer
        item['warnings'].append('Renderer summary extracted; exact symbol layers, picture markers, fonts and cartographic effects may not be portable.')
    except Exception as exc:
        item['warnings'].append('Renderer extraction unavailable: ' + unicode(exc))

    try:
        for cls in layer.labelClasses:
            item['labelClasses'].append({
                'name': getattr(cls, 'className', None),
                'expression': getattr(cls, 'expression', None),
                'expressionEngine': getattr(cls, 'expressionEngine', None),
                'showClassLabels': getattr(cls, 'showClassLabels', None),
                'SQLQuery': getattr(cls, 'SQLQuery', None),
                'minScale': getattr(cls, 'minScale', None),
                'maxScale': getattr(cls, 'maxScale', None)
            })
    except Exception as exc:
        item['warnings'].append('Label class extraction unavailable: ' + unicode(exc))

    layer_dir = os.path.join(output_dir, 'layers')
    if not os.path.isdir(layer_dir):
        os.makedirs(layer_dir)
    filename = '%03d_%s.geojson' % (index, safe_name(item['name']))
    target = os.path.join(layer_dir, filename)
    try:
        arcpy.conversion.FeaturesToJSON(
            layer, target, 'NOT_FORMATTED', 'NO_Z_VALUES', 'NO_M_VALUES',
            'GEOJSON', 'WGS84'
        )
        item['export'] = {
            'status': 'ok',
            'format': 'GeoJSON',
            'path': 'layers/' + filename,
            'outputCoordinateSystem': 'WGS84'
        }
        try:
            with open(target, 'rb') as handle:
                exported = json.load(handle)
            item['export']['exportedFeatureCount'] = len(exported.get('features', []))
            item['export']['countMatches'] = item['featureCount'] == item['export']['exportedFeatureCount']
            if not item['export']['countMatches']:
                item['warnings'].append('Source and exported feature counts differ; inspect selection, definition query, geometry filters and unsupported features.')
            source_fields = [f['name'] for f in item['fields']]
            props = exported.get('features', [{}])[0].get('properties', {}) if exported.get('features') else {}
            item['export']['exportedPropertyNames'] = sorted(props.keys())
            item['export']['sourceFieldNames'] = source_fields
            item['export']['fieldsPresentInSample'] = sorted(set(source_fields).intersection(props.keys()))
            item['export']['fieldsMissingInSample'] = sorted(set(source_fields).difference(props.keys()))
        except Exception as exc:
            item['warnings'].append('Could not validate exported GeoJSON: ' + unicode(exc))
    except Exception as exc:
        item['export'] = {'status': 'failed', 'error': unicode(exc)}
        item['warnings'].append('GeoJSON export failed: ' + unicode(exc))
    return item

def inspect_geodatabase(workspace):
    catalog = {
        'path': workspace,
        'domains': [],
        'featureDatasets': [],
        'featureClasses': [],
        'relationshipClasses': [],
        'topologies': [],
        'warnings': []
    }
    try:
        for domain in arcpy.da.ListDomains(workspace):
            catalog['domains'].append({
                'name': getattr(domain, 'name', None),
                'description': getattr(domain, 'description', None),
                'fieldType': getattr(domain, 'type', None),
                'domainType': getattr(domain, 'domainType', None),
                'mergePolicy': getattr(domain, 'mergePolicy', None),
                'splitPolicy': getattr(domain, 'splitPolicy', None),
                'codedValues': getattr(domain, 'codedValues', None),
                'range': getattr(domain, 'range', None)
            })
    except Exception as exc:
        catalog['warnings'].append('Domain catalog unavailable: ' + unicode(exc))
    try:
        datasets = arcpy.ListDatasets('', 'Feature') or []
        catalog['featureDatasets'] = list(datasets)
        for dataset in datasets:
            try:
                desc = arcpy.Describe(os.path.join(workspace, dataset))
                for child in getattr(desc, 'children', []):
                    if getattr(child, 'dataType', '') == 'Topology':
                        catalog['topologies'].append({
                            'name': getattr(child, 'name', None),
                            'catalogPath': getattr(child, 'catalogPath', None),
                            'clusterTolerance': getattr(child, 'clusterTolerance', None),
                            'maximumGeneratedErrorCount': getattr(child, 'maximumGeneratedErrorCount', None),
                            'rules': 'ArcPy Describe does not expose a portable full rule/error export in this worker.'
                        })
            except Exception as exc:
                catalog['warnings'].append('Dataset inspection failed for ' + unicode(dataset) + ': ' + unicode(exc))
    except Exception as exc:
        catalog['warnings'].append('Feature dataset catalog unavailable: ' + unicode(exc))
    try:
        arcpy.env.workspace = workspace
        classes = arcpy.ListFeatureClasses() or []
        for dataset in catalog['featureDatasets']:
            arcpy.env.workspace = os.path.join(workspace, dataset)
            classes.extend([os.path.join(dataset, fc) for fc in (arcpy.ListFeatureClasses() or [])])
        arcpy.env.workspace = workspace
        for fc in classes:
            full = os.path.join(workspace, fc)
            record = {'name': fc, 'subtypes': [], 'fields': [], 'featureCount': None}
            try:
                record['fields'] = [field_info(f) for f in arcpy.ListFields(full)]
                record['featureCount'] = int(arcpy.GetCount_management(full).getOutput(0))
                for code, subtype in arcpy.da.ListSubtypes(full).items():
                    record['subtypes'].append({
                        'code': code,
                        'name': subtype.get('Name'),
                        'default': subtype.get('Default'),
                        'domains': subtype.get('Domains')
                    })
            except Exception as exc:
                record['warnings'] = [unicode(exc)]
            catalog['featureClasses'].append(record)
        arcpy.env.workspace = workspace
        for rel in (arcpy.ListDatasets('', 'RelationshipClass') or []):
            catalog['relationshipClasses'].append(rel)
    except Exception as exc:
        catalog['warnings'].append('Feature class/subtype/relationship inspection incomplete: ' + unicode(exc))
    return catalog

def discover_geodatabases(layers):
    found = set()
    for layer in layers:
        source = layer.get('dataSource')
        if not source:
            continue
        normalized = os.path.normpath(source)
        parts = normalized.split(os.sep)
        for i, part in enumerate(parts):
            if part.lower().endswith('.gdb'):
                path = os.sep.join(parts[:i + 1])
                if path:
                    found.add(path)
                break
    result = []
    for workspace in sorted(found):
        try:
            result.append(inspect_geodatabase(workspace))
        except Exception as exc:
            result.append({'path': workspace, 'warnings': [unicode(exc)]})
    return result

def main():
    if len(sys.argv) < 3:
        raise RuntimeError('Usage: ExportArcMap108.py <input.mxd> <output-directory>')
    mxd_path = os.path.abspath(sys.argv[1])
    output_dir = os.path.abspath(sys.argv[2])
    if not os.path.isfile(mxd_path):
        raise RuntimeError('MXD not found: ' + mxd_path)
    if os.path.exists(output_dir):
        shutil.rmtree(output_dir)
    os.makedirs(output_dir)
    mxd = arcpy.mapping.MapDocument(mxd_path)
    manifest = {
        'format': 'sabzroyan-arcmap-export',
        'formatVersion': 1,
        'createdUtc': datetime.datetime.utcnow().isoformat() + 'Z',
        'worker': 'ArcMap Desktop 10.8 / ArcPy Python 2.7',
        'sourceDocument': os.path.basename(mxd_path),
        'document': {
            'title': getattr(mxd, 'title', None),
            'author': getattr(mxd, 'author', None),
            'summary': getattr(mxd, 'summary', None),
            'description': getattr(mxd, 'description', None),
            'credits': getattr(mxd, 'credits', None),
            'constraints': getattr(mxd, 'constraints', None),
            'activeView': getattr(mxd, 'activeView', None),
            'dataFrames': []
        },
        'layers': [],
        'geodatabases': [],
        'pageLayout': {'elements': [], 'note': 'Layout extraction is best-effort; exact page rendering is not serialized.'},
        'validation': {'sourceFeatureCount': 0, 'exportedFeatureCount': 0, 'countMismatches': [], 'fieldMismatches': []},
        'knownLimitations': [
            'GeoJSON does not preserve FileGDB domains, subtypes, relationship classes, attachments, topology rules/errors, network datasets, raster pyramids or all source field semantics.',
            'Renderer and label metadata are recorded as a portable summary; exact ArcMap symbol rendering, fonts, placement engine, map surrounds, page layout and dynamic behavior require separate reconstruction.',
            'Broken data sources are reported and cannot be exported until repaired.',
            'Feature count validation is against the ArcMap layer as configured, including its current definition query/selection behavior.'
        ]
    }
    try:
        try:
            for element in arcpy.mapping.ListLayoutElements(mxd):
                entry = {
                    'name': getattr(element, 'name', None),
                    'type': getattr(element, 'type', None),
                    'elementPositionX': getattr(element, 'elementPositionX', None),
                    'elementPositionY': getattr(element, 'elementPositionY', None),
                    'elementWidth': getattr(element, 'elementWidth', None),
                    'elementHeight': getattr(element, 'elementHeight', None),
                    'text': getattr(element, 'text', None),
                    'visible': getattr(element, 'visible', None)
                }
                manifest['pageLayout']['elements'].append(entry)
        except Exception as exc:
            manifest['pageLayout']['warnings'] = [unicode(exc)]
        for df in arcpy.mapping.ListDataFrames(mxd):
            manifest['document']['dataFrames'].append({
                'name': df.name,
                'spatialReference': getattr(getattr(df, 'spatialReference', None), 'name', None),
                'scale': getattr(df, 'scale', None),
                'rotation': getattr(df, 'rotation', None),
                'extent': {
                    'xmin': df.extent.XMin, 'ymin': df.extent.YMin,
                    'xmax': df.extent.XMax, 'ymax': df.extent.YMax
                } if getattr(df, 'extent', None) else None
            })
            for idx, layer in enumerate(arcpy.mapping.ListLayers(mxd, '', df), 1):
                try:
                    record = describe_layer(layer, len(manifest['layers']) + 1, output_dir)
                    record['dataFrame'] = df.name
                    manifest['layers'].append(record)
                except Exception as exc:
                    manifest['layers'].append({
                        'index': len(manifest['layers']) + 1,
                        'name': getattr(layer, 'name', 'unknown'),
                        'export': {'status': 'failed'},
                        'warnings': [unicode(exc), traceback.format_exc()]
                    })
        manifest['geodatabases'] = discover_geodatabases(manifest['layers'])
        for layer in manifest['layers']:
            if isinstance(layer.get('featureCount'), (int, long)):
                manifest['validation']['sourceFeatureCount'] += layer['featureCount']
            exported = layer.get('export', {}).get('exportedFeatureCount')
            if isinstance(exported, (int, long)):
                manifest['validation']['exportedFeatureCount'] += exported
            if layer.get('export', {}).get('countMatches') is False:
                manifest['validation']['countMismatches'].append({
                    'layer': layer.get('name'),
                    'source': layer.get('featureCount'),
                    'exported': exported
                })
            if layer.get('export', {}).get('fieldsMissingInSample'):
                manifest['validation']['fieldMismatches'].append({
                    'layer': layer.get('name'),
                    'missingFromSample': layer['export']['fieldsMissingInSample']
                })
        manifest_path = os.path.join(output_dir, 'manifest.json')
        with open(manifest_path, 'wb') as handle:
            handle.write(json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8'))
    finally:
        del mxd

    package_path = output_dir.rstrip(os.sep) + '.zip'
    with zipfile.ZipFile(package_path, 'w', zipfile.ZIP_DEFLATED) as archive:
        for root, dirs, files in os.walk(output_dir):
            for filename in files:
                full = os.path.join(root, filename)
                archive.write(full, os.path.relpath(full, output_dir))
    print(json.dumps({
        'status': 'ok',
        'package': package_path,
        'manifest': os.path.join(output_dir, 'manifest.json'),
        'layerCount': len(manifest['layers']),
        'countMismatches': len(manifest['validation']['countMismatches']),
        'fieldMismatches': len(manifest['validation']['fieldMismatches'])
    }, ensure_ascii=False, indent=2))

if __name__ == '__main__':
    main()
