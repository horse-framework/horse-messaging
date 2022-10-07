## BEGIN INIT INFO
# Provides:          horse
# Required-Start:    $local_fs $network
# Required-Stop:     $local_fs
# Default-Start:     2 3 4 5
# Default-Stop:      0 1 6
# Short-Description: horse
# Description:       Horse Messaging Server
### END INIT INFO

case "$1" in
  start)
    echo "Starting Horse"
    sudo start-stop-daemon --start --oknodo --background --pidfile /run/horse.pid --make-pidfile --exec /usr/bin/dotnet /opt/horse/HorseService.dll
    ;;
  stop)
    echo "Stopping Horse"
    sudo start-stop-daemon --stop --oknodo --pidfile /run/horse.pid
    sleep 2
    ;;
  *)
    echo "Usage: /etc/init.d/horse {start|stop}"
    exit 1
    ;;
esac

exit 0