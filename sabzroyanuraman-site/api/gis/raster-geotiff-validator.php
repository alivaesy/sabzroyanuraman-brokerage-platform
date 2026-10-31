<?php
function raster_u16(string $b,int $o,bool $le): int {
 if($o<0||$o+2>strlen($b))throw new RuntimeException('invalid_geotiff_structure');
 $v=unpack($le?'v':'n',substr($b,$o,2));return (int)$v[1];
}
function raster_u32(string $b,int $o,bool $le): int {
 if($o<0||$o+4>strlen($b))throw new RuntimeException('invalid_geotiff_structure');
 $v=unpack($le?'V':'N',substr($b,$o,4));return (int)$v[1];
}
function raster_validate_geotiff(string $path,array $metadata): void {
 $size=filesize($path);if($size===false||$size<14)throw new RuntimeException('invalid_geotiff_structure');
 $h=file_get_contents($path,false,null,0,8);
 if(strlen($h)!==8)throw new RuntimeException('invalid_geotiff_structure');
 $le=substr($h,0,2)==='II';
 if(!$le&&substr($h,0,2)!=='MM')throw new RuntimeException('invalid_geotiff_structure');
 // Classic TIFF only; BigTIFF is rejected until its 64-bit IFD parser is available.
 if(raster_u16($h,2,$le)!==42)throw new RuntimeException('unsupported_or_invalid_tiff_variant');
 $ifd=raster_u32($h,4,$le);if($ifd<8||$ifd+2>$size)throw new RuntimeException('invalid_geotiff_ifd');
 $count=raster_u16(file_get_contents($path,false,null,$ifd,2)?:'',0,$le);
 if($count<1||$count>4096||$ifd+2+($count*12)+4>$size)throw new RuntimeException('invalid_geotiff_ifd');
 $fp=fopen($path,'rb');if(!$fp)throw new RuntimeException('invalid_geotiff_structure');
 $entries=[];$typeSize=[1=>1,2=>1,3=>2,4=>4,5=>8,6=>1,7=>1,8=>2,9=>4,10=>8,11=>4,12=>8];
 for($i=0;$i<$count;$i++){
  fseek($fp,$ifd+2+$i*12);$e=fread($fp,12);if(strlen($e)!==12){fclose($fp);throw new RuntimeException('invalid_geotiff_ifd');}
  $tag=raster_u16($e,0,$le);$type=raster_u16($e,2,$le);$n=raster_u32($e,4,$le);
  if(!isset($typeSize[$type])||$n<1||$n>100000000){fclose($fp);throw new RuntimeException('invalid_geotiff_tag');}
  $bytes=$typeSize[$type]*$n;$pos=$bytes<=4?($ifd+2+$i*12+8):raster_u32($e,8,$le);
  if($pos<0||$pos+$bytes>$size){fclose($fp);throw new RuntimeException('invalid_geotiff_tag_offset');}
  $entries[$tag]=['type'=>$type,'count'=>$n,'pos'=>$pos,'bytes'=>$bytes];
 }
 $values=function(int $tag)use($entries,$fp,$le):array{
  if(!isset($entries[$tag]))return [];
  $e=$entries[$tag];fseek($fp,$e['pos');$raw=fread($fp,$e['bytes');if(strlen($raw)!==$e['bytes'])return [];
  $out=[];$step=$e['type']===3?2:($e['type']===4?4:0);
  if($step===0)return [];
  for($j=0;$j<$e['count'];$j++)$out[]=$step===2?raster_u16($raw,$j*2,$le):raster_u32($raw,$j*4,$le);
  return $out;
 };
 $w=$values(256)[0]??0;$hgt=$values(257)[0]??0;
 $offsets=$values(273);$lengths=$values(279);
 if(!$offsets||!$lengths){$offsets=$values(324);$lengths=$values(325);}
 $hasTransform=isset($entries[34264])&&$entries[34264]['count']>=16;
 $hasTieScale=isset($entries[33922])&&$entries[33922]['count']>=6&&isset($entries[33550])&&$entries[33550]['count']>=3;
 $geoKeys=$values(34735);$tiffEpsg=0;
 if(count($geoKeys)>=4){
  $keyCount=min((int)$geoKeys[3],256);
  for($k=0;$k<$keyCount;$k++){
   $base=4+$k*4;if(!isset($geoKeys[$base+3]))break;
   if(in_array($geoKeys[$base], [2048,3072], true)&&$geoKeys[$base+1]===0&&$geoKeys[$base+2]===1){$tiffEpsg=(int)$geoKeys[$base+3];break;}
  }
 }
 if($w<1||$hgt<1||$w>1000000||$hgt>1000000||$w*$hgt>100000000||
    $w!==$metadata['width']||$hgt!==$metadata['height']||$tiffEpsg!==(int)$metadata['crs']['epsg']||
    !$hasKeys||(!$hasTransform&&!$hasTieScale)||!$offsets||count($offsets)!==count($lengths)){
  fclose($fp);throw new RuntimeException('geotiff_metadata_or_image_structure_invalid');
 }
 foreach($offsets as $i=>$offset)if($lengths[$i]<1||$offset<8||$offset+$lengths[$i]>$size){
  fclose($fp);throw new RuntimeException('geotiff_image_block_out_of_bounds');
 }
 fclose($fp);
}

