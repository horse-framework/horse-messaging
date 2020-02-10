import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConnectionsComponent } from './connections.component';
import { RouterModule } from '@angular/router';

@NgModule({
    declarations: [ConnectionsComponent],
    imports: [
        CommonModule,
        RouterModule.forChild([{ path: '', component: ConnectionsComponent }])
    ]
})
export class ConnectionsModule { }
