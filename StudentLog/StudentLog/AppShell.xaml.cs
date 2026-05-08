namespace StudentLog
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("studenthistory", typeof(UI.Views.StudentHistoryPage));
        }
    }
}
