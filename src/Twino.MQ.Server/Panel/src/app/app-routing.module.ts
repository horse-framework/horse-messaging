import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

const routes: Routes = [
    {
        path: 'dashboard',
        loadChildren: './dashboard/dashboard.module#DashboardModule'
    },
    {
        path: 'connections',
        loadChildren: './connections/connections.module#ConnectionsModule'
    },
    {
        path: 'channels',
        loadChildren: './channels/channels.module#ChannelsModule'
    },
    {
        path: 'administration',
        loadChildren: './administration/administration.module#AdministrationModule'
    },
    {
        path: '',
        redirectTo: '/dashboard',
        pathMatch: 'full'
    }
];

@NgModule({
    imports: [RouterModule.forRoot(routes)],
    exports: [RouterModule]
})
export class AppRoutingModule { }
