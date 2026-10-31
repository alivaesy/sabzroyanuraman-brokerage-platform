/* tslint:disable */
/* eslint-disable */

export class Point {
    free(): void;
    [Symbol.dispose](): void;
    constructor(x: number, y: number, z: number);
    x: number;
    y: number;
    z: number;
}

export class Projection {
    free(): void;
    [Symbol.dispose](): void;
    constructor(defn: string);
    readonly axis: string;
    readonly isGeocentric: boolean;
    readonly isLatlon: boolean;
    readonly isNormalizedAxis: boolean;
    readonly projName: string;
    readonly to_meter: number;
    readonly units: string;
}

export function adapter_protocol_version(): number;

export function adapter_status(): string;

export function add_nadgrid(key: string, array: Uint8Array): void;

/**
 * Parses the official RustGIS .rgplugin ZIP format and validates its manifest and WASM ABI.
 */
export function inspect_plugin_package(bytes: Uint8Array | number[]): string;

export function install_plugin_package(bytes: Uint8Array | number[]): string;

/**
 * Returns installed sandboxed plugins.
 */
export function list_plugins(): string;

/**
 * Returns the actual RustGIS analysis tool catalogue and parameter definitions.
 */
export function list_tools(): string;

/**
 * Executes a plugin command with NoHost. The sandbox has no ambient imports; all host calls
 * fail closed until a reviewed, permission-aware WebGIS Host implementation is provided.
 */
export function run_plugin_command(command_id: string, params_json: string): string;

/**
 * Runs a RustGIS rasterops algorithm and returns a georeferenced GeoTIFF.
 */
export function run_raster_tool(tool_id: string, input_base64: string, input_name: string, params_json: string): string;

/**
 * Runs a real RustGIS analysis tool.
 *
 * `layers_json` is an object mapping layer names to GeoJSON FeatureCollections.
 * `params_json` uses the parameter names from RustGIS's ToolSpec catalogue.
 */
export function run_tool(tool_id: string, layers_json: string, params_json: string): string;

/**
 * Enables or disables an installed plugin by its manifest id.
 */
export function set_plugin_enabled(id: string, enabled: boolean): void;

export function transform(src: Projection, dst: Projection, point: Point): void;

/**
 * Removes an installed plugin by its manifest id.
 */
export function uninstall_plugin(id: string): boolean;

export function upstream_revision(): string;

export type InitInput = RequestInfo | URL | Response | BufferSource | WebAssembly.Module;

export interface InitOutput {
    readonly memory: WebAssembly.Memory;
    readonly __wbg_get_point_x: (a: number) => number;
    readonly __wbg_get_point_y: (a: number) => number;
    readonly __wbg_get_point_z: (a: number) => number;
    readonly __wbg_point_free: (a: number, b: number) => void;
    readonly __wbg_projection_free: (a: number, b: number) => void;
    readonly __wbg_set_point_x: (a: number, b: number) => void;
    readonly __wbg_set_point_y: (a: number, b: number) => void;
    readonly __wbg_set_point_z: (a: number, b: number) => void;
    readonly adapter_protocol_version: () => number;
    readonly adapter_status: () => [number, number];
    readonly add_nadgrid: (a: number, b: number, c: any) => [number, number];
    readonly inspect_plugin_package: (a: number, b: number) => [number, number, number, number];
    readonly install_plugin_package: (a: number, b: number) => [number, number, number, number];
    readonly list_plugins: () => [number, number, number, number];
    readonly list_tools: () => [number, number, number, number];
    readonly point_new: (a: number, b: number, c: number) => number;
    readonly projection_axis: (a: number) => [number, number];
    readonly projection_isGeocentric: (a: number) => number;
    readonly projection_isLatlon: (a: number) => number;
    readonly projection_isNormalizedAxis: (a: number) => number;
    readonly projection_new: (a: number, b: number) => [number, number, number];
    readonly projection_projName: (a: number) => [number, number];
    readonly projection_to_meter: (a: number) => number;
    readonly projection_units: (a: number) => [number, number];
    readonly run_plugin_command: (a: number, b: number, c: number, d: number) => [number, number, number, number];
    readonly run_raster_tool: (a: number, b: number, c: number, d: number, e: number, f: number, g: number, h: number) => [number, number, number, number];
    readonly run_tool: (a: number, b: number, c: number, d: number, e: number, f: number) => [number, number, number, number];
    readonly set_plugin_enabled: (a: number, b: number, c: number) => [number, number];
    readonly transform: (a: number, b: number, c: number) => [number, number];
    readonly uninstall_plugin: (a: number, b: number) => number;
    readonly upstream_revision: () => [number, number];
    readonly rust_zstd_wasm_shim_calloc: (a: number, b: number) => number;
    readonly rust_zstd_wasm_shim_free: (a: number) => void;
    readonly rust_zstd_wasm_shim_malloc: (a: number) => number;
    readonly rust_zstd_wasm_shim_memcmp: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_memcpy: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_memmove: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_memset: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_qsort: (a: number, b: number, c: number, d: number) => void;
    readonly __wbindgen_externrefs: WebAssembly.Table;
    readonly __wbindgen_free: (a: number, b: number, c: number) => void;
    readonly __wbindgen_malloc: (a: number, b: number) => number;
    readonly __wbindgen_realloc: (a: number, b: number, c: number, d: number) => number;
    readonly __externref_table_dealloc: (a: number) => void;
    readonly __wbindgen_start: () => void;
}

export type SyncInitInput = BufferSource | WebAssembly.Module;

/**
 * Instantiates the given `module`, which can either be bytes or
 * a precompiled `WebAssembly.Module`.
 *
 * @param {{ module: SyncInitInput }} module - Passing `SyncInitInput` directly is deprecated.
 *
 * @returns {InitOutput}
 */
export function initSync(module: { module: SyncInitInput } | SyncInitInput): InitOutput;

/**
 * If `module_or_path` is {RequestInfo} or {URL}, makes a request and
 * for everything else, calls `WebAssembly.instantiate` directly.
 *
 * @param {{ module_or_path: InitInput | Promise<InitInput> }} module_or_path - Passing `InitInput` directly is deprecated.
 *
 * @returns {Promise<InitOutput>}
 */
export default function __wbg_init (module_or_path?: { module_or_path: InitInput | Promise<InitInput> } | InitInput | Promise<InitInput>): Promise<InitOutput>;
