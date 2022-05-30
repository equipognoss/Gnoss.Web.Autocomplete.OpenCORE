![](https://content.gnoss.ws/imagenes/proyectos/personalizacion/7e72bf14-28b9-4beb-82f8-e32a3b49d9d3/cms/logognossazulprincipal.png)

# Gnoss.Web.Autocomplete.OpenCORE

Aplicación Web que se encarga de generar las sugerencias de búsqueda de una faceta concreta. Por ejemplo, si en la faceta (o filtro de búsqueda) de país, el usuario empieza a teclear “espa”, esta aplicación le sugiere “España”.

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
autocomplete:
    image: autocomplete
    env_file: .env
    ports:
     - ${puerto_autocompletar}:80
    environment:
     virtuosoConnectionString: ${virtuosoConnectionString}
     acid: ${acid}
     base: ${base}
     redis__redis__ip__master: ${redis__redis__ip__master}
     redis__redis__ip__read: ${redis__redis__ip__read}
     redis__redis__bd: ${redis__redis__bd}
     redis__redis__timeout: ${redis__redis__timeout}
     redis__recursos__ip__master: ${redis__recursos__ip__master}
     redis__recursos__ip__read: ${redis__recursos__ip__read}
     redis__recursos__bd: ${redis__recursos__bd}
     redis__recursos__timeout: ${redis__redis__timeout}
     idiomas: ${idiomas}
     Servicios__urlBase: ${Servicios__urlBase}
     connectionType: ${connectionType}
    volumes:
      - ./logs/autocomplete:/app/logs
```

Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.Platform.Deploy
