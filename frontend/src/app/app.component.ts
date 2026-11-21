import { Component, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as L from 'leaflet';

interface Church { id:number; name:string; latitude:number; longitude:number; address:string|null; website:string|null; confession:string|null; jurisdiction:string|null; accessible:boolean|null; parking:boolean|null; distanceKm:number; osmKey:string|null; celebrations:{rite:string|null;type:string|null;language:string|null;dayOfWeek:number;time:string;sourceUrl:string}[] }
@Component({ selector:'app-root', standalone:true, imports:[CommonModule,FormsModule], templateUrl:'./app.component.html', styleUrl:'./app.component.scss' })
export class AppComponent implements AfterViewInit {
 private map!:L.Map; private markers=L.layerGroup(); private centerMarker?:L.CircleMarker;
 churches:Church[]=[]; selected?:Church; loading=false; error=''; moved=false; located=false;
 lat=50.6326; lon=5.5797; radius=10; confession=''; jurisdiction=''; rite=''; type=''; language=''; day=''; from=''; to=''; accessible=false; parking=false; demo=false; advanced=false;
 days=['dimanche','lundi','mardi','mercredi','jeudi','vendredi','samedi'];
 ngAfterViewInit() {
  this.map=L.map('map',{zoomControl:false}).setView([this.lat,this.lon],12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'}).addTo(this.map);
  L.control.zoom({position:'bottomright'}).addTo(this.map); this.markers.addTo(this.map);
  this.map.on('moveend',()=>{this.moved=true}); this.search();
 }
 async search(useMap=false) {
  if(useMap){const c=this.map.getCenter();this.lat=c.lat;this.lon=c.lng;this.located=false;}
  this.moved=false;this.loading=true;this.error='';this.selected=undefined;
  const p=new URLSearchParams({lat:String(this.lat),lon:String(this.lon),radius:String(this.radius)});
  for(const [key,value] of Object.entries({confession:this.confession,jurisdiction:this.jurisdiction,rite:this.rite,type:this.type,language:this.language,day:this.day,from:this.from,to:this.to})) if(value) p.set(key,value);
  if(this.accessible)p.set('accessible','true');if(this.parking)p.set('parking','true');if(this.demo)p.set('dataset','demo');
  try { const response=await fetch('http://localhost:5100/api/churches?'+p);if(!response.ok)throw Error();this.churches=await response.json();this.draw(); }
  catch {this.error="Impossible de joindre les données. Vérifiez que l’API et PostgreSQL sont démarrés.";this.churches=[];this.markers.clearLayers();}
  finally {this.loading=false;}
 }
 draw(){this.markers.clearLayers();for(const c of this.churches){const marker=L.circleMarker([c.latitude,c.longitude],{radius:8,color:'#fff',weight:3,fillColor:'#a3583d',fillOpacity:1}).addTo(this.markers);marker.on('click',()=>this.select(c));} if(this.centerMarker)this.centerMarker.remove();this.centerMarker=L.circleMarker([this.lat,this.lon],{radius:7,color:'#fff',weight:3,fillColor:'#284f4b',fillOpacity:1}).addTo(this.map);}
 select(c:Church){this.selected=c;this.map.panTo([c.latitude,c.longitude]);}
 locate(){if(!navigator.geolocation){this.error="La géolocalisation n’est pas disponible dans ce navigateur.";return;}navigator.geolocation.getCurrentPosition(p=>{this.lat=p.coords.latitude;this.lon=p.coords.longitude;this.located=true;this.map.setView([this.lat,this.lon],13);this.search();},()=>{this.error="Position indisponible ou autorisation refusée. Vous pouvez déplacer la carte.";},{timeout:10000});}
 directions(c:Church){return 'https://www.openstreetmap.org/directions?to='+c.latitude+'%2C'+c.longitude;}
 validUrl(url:string|null){try{const u=new URL(url||'');return u.protocol==='https:'||u.protocol==='http:'}catch{return false}}
}
