using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace ERP.Core.Compartido.Controles
{
    /// <summary>
    /// Control de barra de herramientas personalizado con botones de navegacion y acciones
    /// </summary>
    public partial class SasToolBar : UserControl
    {
        #region Campos

        /// <summary>
        /// Numero del boton presionado (1-8)
        /// </summary>
        public int ButtonPressed = 0;

        private Image _imagen;
        private int _estilo = 1;

        #endregion

        #region Eventos

        /// <summary>
        /// Evento que se dispara cuando se hace clic en cualquier boton de la barra
        /// </summary>
        public event EventHandler ClickEvent;

        #endregion

        #region Constructor

        public SasToolBar()
        {
            InitializeComponent();
        }

        #endregion

        #region Propiedades de Imagenes

        /// <summary>
        /// Imagen del boton Salir
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Salir
        {
            get { return Button2.BackgroundImage; }
            set { Button2.BackgroundImage = value; }
        }

        /// <summary>
        /// Imagen del boton Grabar
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Grabar
        {
            get { return Button3.BackgroundImage; }
            set { Button3.BackgroundImage = value; }
        }

        /// <summary>
        /// Imagen del boton Eliminar
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Eliminar
        {
            get { return Button4.BackgroundImage; }
            set { Button4.BackgroundImage = value; }
        }

        /// <summary>
        /// Imagen del boton Primero
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Primero
        {
            get { return Button5.BackgroundImage; }
            set { Button5.BackgroundImage = value; }
        }

        /// <summary>
        /// Imagen del boton Atras
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Atras
        {
            get { return Button6.BackgroundImage; }
            set { Button6.BackgroundImage = value; }
        }

        /// <summary>
        /// Imagen del boton Siguiente
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Siguiente
        {
            get { return Button7.BackgroundImage; }
            set { Button7.BackgroundImage = value; }
        }

        /// <summary>
        /// Imagen del boton Ultimo
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Imagen_Ultimo
        {
            get { return Button8.BackgroundImage; }
            set
            {
                _imagen = value;
                Button8.BackgroundImage = value;
            }
        }

        #endregion

        #region Propiedades de Estilo

        /// <summary>
        /// Estilo de la barra de herramientas (0-3)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Estilo_Barra
        {
            get { return _estilo; }
            set
            {
                if (value < 0 || value > 3)
                {
                    MessageBox.Show("Valores de 0 a 3", "Informacion", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _estilo = value;
                FlatStyle flatStyle = (FlatStyle)value;

                Button1.FlatStyle = flatStyle;
                Button2.FlatStyle = flatStyle;
                Button3.FlatStyle = flatStyle;
                Button4.FlatStyle = flatStyle;
                Button5.FlatStyle = flatStyle;
                Button6.FlatStyle = flatStyle;
                Button7.FlatStyle = flatStyle;
                Button8.FlatStyle = flatStyle;
            }
        }

        #endregion

        #region Propiedades de ToolTips

        /// <summary>
        /// Tooltip del boton 1 (Salir)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton1
        {
            get { return tip.GetToolTip(Button2); }
            set { tip.SetToolTip(Button2, value); }
        }

        /// <summary>
        /// Tooltip del boton 2 (Guardar)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton2
        {
            get { return tip.GetToolTip(Button3); }
            set { tip.SetToolTip(Button3, value); }
        }

        /// <summary>
        /// Tooltip del boton 3 (Eliminar)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton3
        {
            get { return tip.GetToolTip(Button4); }
            set { tip.SetToolTip(Button4, value); }
        }

        /// <summary>
        /// Tooltip del boton 4 (Primero)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton4
        {
            get { return tip.GetToolTip(Button5); }
            set { tip.SetToolTip(Button5, value); }
        }

        /// <summary>
        /// Tooltip del boton 5 (Anterior)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton5
        {
            get { return tip.GetToolTip(Button6); }
            set { tip.SetToolTip(Button6, value); }
        }

        /// <summary>
        /// Tooltip del boton 6 (Siguiente)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton6
        {
            get { return tip.GetToolTip(Button7); }
            set { tip.SetToolTip(Button7, value); }
        }

        /// <summary>
        /// Tooltip del boton 7 (Ultimo)
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Tooltip_Boton7
        {
            get { return tip.GetToolTip(Button8); }
            set { tip.SetToolTip(Button8, value); }
        }

        #endregion

        #region Propiedades de Habilitacion

        /// <summary>
        /// Habilita o deshabilita el boton Salir
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Enabled_Salir
        {
            get { return Button2.Enabled; }
            set { Button2.Enabled = value; }
        }

        /// <summary>
        /// Habilita o deshabilita el boton Grabar
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Enabled_Grabar
        {
            get { return Button3.Enabled; }
            set { Button3.Enabled = value; }
        }

        /// <summary>
        /// Habilita o deshabilita el boton Eliminar
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Enabled_Eliminar
        {
            get { return Button4.Enabled; }
            set { Button4.Enabled = value; }
        }

        #endregion

        #region Manejadores de Eventos

        /// <summary>
        /// Manejador de eventos para todos los botones
        /// </summary>
        private void Botones_ClickEvent(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Name.Length > 0)
            {
                // Obtener el ultimo caracter del nombre del boton (numero)
                string lastChar = button.Name.Substring(button.Name.Length - 1);
                if (int.TryParse(lastChar, out int buttonNumber))
                {
                    ButtonPressed = buttonNumber;
                }
            }

            // Disparar el evento ClickEvent
            ClickEvent?.Invoke(sender, e);
        }

        /// <summary>
        /// Manejador del evento Resize para mantener la altura fija
        /// </summary>
        private void SasToolBar_Resize(object sender, EventArgs e)
        {
            this.Height = 27;
        }

        #endregion
    }
}
